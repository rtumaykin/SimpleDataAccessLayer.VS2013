using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using EnvDTE;
using Microsoft.SqlServer.Management.Common;

namespace SimpleDataAccessLayer_vs2013
{
	public class Helper
	{
		private readonly DTE _dte;
		private readonly String _templateFileName;

		public Helper(DTE dte, String templateFileName)
		{
			_dte = dte;
			_templateFileName = templateFileName;
		}

		private ProjectItem _templateFile;
		public ProjectItem TemplateFile
		{
			get
			{
				System.Threading.LazyInitializer.EnsureInitialized(
					ref _templateFile,
					() =>
					{
						try
						{
							return _dte.Solution.FindProjectItem(_templateFileName);
						}
						catch
						{
							return (ProjectItem)null;
						}
					}
				);
				return _templateFile;
			}
		}

		private DalConfig _config;
		public DalConfig Config
		{
			get
			{
				System.Threading.LazyInitializer.EnsureInitialized(
					ref _config,
					() =>
					{
						try
						{
							var xmlProjectItem = (ProjectItem) TemplateFile.Collection.Parent;
							var ser = new DataContractSerializer(typeof(DalConfig));
							var fileName = xmlProjectItem.FileNames[0];
							var dalConfig = (DalConfig)ser.ReadObject(XmlReader.Create(fileName));
							return dalConfig;
						}
						catch
						{
							return new DalConfig();
						}
					}
				);
				return _config;
			}
		}

		private string _designerConnectionString;
		public string DesignerConnectionString
		{
			get
			{
				System.Threading.LazyInitializer.EnsureInitialized(
					ref _designerConnectionString,
					() =>
					{
						try
						{
							var activeProject = TemplateFile.ContainingProject;

							var configurationFilename = (from ProjectItem item in activeProject.ProjectItems where Regex.IsMatch(item.Name, "(app|web).config", RegexOptions.IgnoreCase) select item.FileNames[0]).FirstOrDefault();

						    if (!string.IsNullOrEmpty(configurationFilename))
							{
								// found it, map it and expose salient members as properties
							    var configFile = new ExeConfigurationFileMap {ExeConfigFilename = configurationFilename};
							    var configuration = System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(configFile, ConfigurationUserLevel.None);
								var configConnectionString = configuration.ConnectionStrings.ConnectionStrings[Config.ApplicationConnectionString].ConnectionString;
								var sb = new SqlConnectionStringBuilder(configConnectionString);
								if (Config.DesignerConnection.Authentication is WindowsAuthentication)
								{
									sb.IntegratedSecurity = true;
								}
								else
								{
									var auth = Config.DesignerConnection.Authentication as SqlAuthentication;
									sb.IntegratedSecurity = false;
								    if (auth != null)
								    {
								        sb.UserID = auth.UserName;
								        sb.Password = auth.Password;
								    }
								}
								return sb.ConnectionString;

							}
							else
							{
								return (string)null;
							}
						}
						catch
						{
							return (string)null;
						}
					}
				);
				return _designerConnectionString;
			}
		}

		public List<ProcedureParameter> GetProcedureParameterCollection(string objectSchemaName, string objectName)
		{
			var fullObjectName = Tools.QuoteName(objectSchemaName) + "." + Tools.QuoteName(objectName);
			var retValue = new List<ProcedureParameter>();

			using (var conn = new SqlConnection(DesignerConnectionString))
			{
				conn.Open();

				using (var cmd = conn.CreateCommand())
				{
					cmd.CommandType = CommandType.StoredProcedure;
					cmd.CommandText = "sp_executesql";

					const string stmt = @"
					SELECT 
						p.[name] AS ParameterName,
						p.[max_length] AS MaxByteLength,
						p.[precision] AS [Precision],
						p.[scale] AS Scale,
						p.[is_output] AS IsOutputParameter,
						ISNULL(ut.name, t.[name]) AS TypeName
					FROM sys.[parameters] p
						INNER JOIN sys.[types] t
							ON	t.[user_type_id] = p.[user_type_id]
						LEFT OUTER JOIN sys.[types] ut
							ON	ut.[user_type_id] = t.[system_type_id]
					WHERE p.[object_id] = OBJECT_ID(@ObjectName);
					";

					cmd.Parameters.AddWithValue("@stmt", stmt);
					cmd.Parameters.AddWithValue("@params", "@ObjectName sysname");
					cmd.Parameters.AddWithValue("@ObjectName", fullObjectName);

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							// Remove @ from the beginning of the parameter
							var parameterName = reader.GetSqlString(0).Value.Substring(1);
							var sqlTypeName = reader.GetSqlString(5).Value;
							int maxByteLength = reader.GetSqlInt16(1).Value;
							var precision = reader.GetSqlByte(2).Value;
							var scale = reader.GetSqlByte(3).Value;
							var isOutputParameter = reader.GetSqlBoolean(4).Value;

							var clrTypeName = Tools.ClrTypeName(sqlTypeName);

							if (string.IsNullOrWhiteSpace(clrTypeName))
								continue; // later I will rewrite it 

							retValue.Add(new ProcedureParameter(parameterName, maxByteLength, precision, scale, isOutputParameter, sqlTypeName));
						}
					}
				}
			}
			return retValue;
		}

		public byte GetDatabaseCompatibilityLevel()
		{
			byte retValue;
			using (var conn = new SqlConnection(DesignerConnectionString))
			{
				conn.Open();

				try
				{
					using (var cmd = conn.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = "sp_executesql";
						cmd.Parameters.AddWithValue("@stmt", @"SELECT @CompatibilityLevel = [compatibility_level] FROM sys.[databases] WHERE [database_id] = DB_ID();");
						cmd.Parameters.AddWithValue("@params", "@CompatibilityLevel tinyint OUTPUT");
						cmd.Parameters.Add(new SqlParameter("@CompatibilityLevel", SqlDbType.TinyInt) { Direction = ParameterDirection.Output });
						cmd.ExecuteNonQuery();
						retValue = (byte)cmd.Parameters["@CompatibilityLevel"].Value;
					}
				}
				catch // (Exception e)
				{
					retValue = 0;
				}
			}
			return retValue;
		}

		public List<List<ProcedureResultSetColumn>> GetProcedureResultSetColumnCollection(string objectSchemaName, string objectName)
		{
			// SQL 2012 still can use FMTONLY so this is only for the higher versions 
			if (GetDatabaseCompatibilityLevel() > 110)
			{
				return GetProcedureResultSetColumnCollection2014(objectSchemaName, objectName);
			}
			else
			{
				return GetProcedureResultSetColumnCollection2005(objectSchemaName, objectName);
			}
		}

		public List<List<ProcedureResultSetColumn>> GetProcedureResultSetColumnCollection2005(string objectSchemaName, string objectName)
		{
			var fullObjectName = Tools.QuoteName(objectSchemaName) + "." + Tools.QuoteName(objectName);
			var retValue = new List<List<ProcedureResultSetColumn>>();
		    try
		    {
		        var sb = new SqlConnectionStringBuilder(DesignerConnectionString);
		        var conn = sb.IntegratedSecurity
		            ? new ServerConnection(sb.DataSource)
		            : new ServerConnection(sb.DataSource, sb.UserID, sb.Password);
		        conn.DatabaseName = sb.InitialCatalog;
		        var procedureCall = "EXEC " + fullObjectName;
		        var isFirstParam = true;
		        foreach (var param in GetProcedureParameterCollection(objectSchemaName, objectName))
		        {
		            procedureCall += (isFirstParam ? " " : ", ") + "@" + param.ParameterName + " = NULL";
		            isFirstParam = false;
		        }

		        var ds = conn.ExecuteWithResults(new StringCollection() {"SET FMTONLY ON;", procedureCall + ";"});
		        retValue.AddRange(from DataTable dt in ds[1].Tables
		            select
		                (from DataColumn column in dt.Columns
		                    select new ProcedureResultSetColumn(column.ColumnName, column.DataType.FullName)).ToList());
		    }
		    catch
		    {
		        // Whatever happens just don't return anything
		        return new List<List<ProcedureResultSetColumn>>();
		    }
		    // GetProcedureParameterCollection
			return retValue;
		}

		public List<List<ProcedureResultSetColumn>> GetProcedureResultSetColumnCollection2014(string objectSchemaName, string objectName)
		{
			var firstRecordset = new List<ProcedureResultSetColumn>();
			var fullObjectName = Tools.QuoteName(objectSchemaName) + "." + Tools.QuoteName(objectName);

			using (var conn = new SqlConnection(DesignerConnectionString))
			{
				conn.Open();

				using (var cmd = conn.CreateCommand())
				{
					cmd.CommandType = CommandType.StoredProcedure;
					cmd.CommandText = "sp_executesql";
					cmd.Parameters.AddWithValue("@stmt", @"
				SELECT 
					c.[name] AS ColumnName,
					c.[precision],
					c.[scale],
					ISNULL(t.[name], tu.[name]) AS TypeName 
				FROM sys.[dm_exec_describe_first_result_set_for_object](OBJECT_ID(@ObjectFullName), 0) c
					LEFT OUTER JOIN sys.[types] t
						ON	t.[user_type_id] = c.[system_type_id]
					LEFT OUTER JOIN sys.[types] tu
						ON	tu.[user_type_id] = c.[user_type_id]
				WHERE c.name IS NOT NULL");
					cmd.Parameters.AddWithValue("@params", "@ObjectFullName sysname");
					cmd.Parameters.AddWithValue("@ObjectFullName", fullObjectName);
					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							firstRecordset.Add(new ProcedureResultSetColumn(reader.GetSqlString(0).Value, Tools.ClrTypeName(reader.GetSqlString(3).Value)));

						}
					}
				}
			}

            return new List<List<ProcedureResultSetColumn>> { firstRecordset };
		}

		public List<EnumData> GetEnumDataCollection(string objectSchemaName, string objectName, string valueColumnName, string keyColumnName)
		{
			var retValue = new List<EnumData>();
			var fullObjectName = Tools.QuoteName(objectSchemaName) + "." + Tools.QuoteName(objectName);

			using (var conn = new SqlConnection(DesignerConnectionString))
			{
				conn.Open();
				using (var cmd = conn.CreateCommand())
				{
					cmd.CommandType = CommandType.StoredProcedure;
					cmd.CommandText = "sp_executesql";

					cmd.Parameters.Add(new SqlParameter("@stmt", String.Format("SELECT CONVERT(bigint, {0}) AS [Value], {1} AS [Key] FROM {2} ORDER BY {0}", valueColumnName, keyColumnName, fullObjectName)));

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							retValue.Add(new EnumData((string)reader["Key"], (long)reader["Value"]));
						}
					}
				}
			}
			return retValue;
		}
	}
}
