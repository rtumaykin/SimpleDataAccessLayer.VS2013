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
	                            ISNULL(st.name, t.[name]) AS TypeName,
	                            SCHEMA_NAME(t.[schema_id]) AS TypeSchemaName,
	                            t.is_table_type
                            FROM sys.[parameters] p
	                            INNER JOIN sys.[types] t
		                            ON	t.[user_type_id] = p.[user_type_id]
	                            LEFT OUTER JOIN sys.[types] st
		                            ON	st.user_type_id = p.[system_type_id]
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
						    var schemaName = reader.GetSqlString(6).Value;
						    var isTableType = reader.GetSqlBoolean(7).Value;

							var clrTypeName = schemaName == "sys" ? 
                                Tools.ClrTypeName(sqlTypeName) : 
                                string.Format("{0}.{1}", Tools.CleanName(schemaName), Tools.CleanName(sqlTypeName));

						    if (!string.IsNullOrWhiteSpace(clrTypeName) || isTableType)
						    {
						        retValue.Add(new ProcedureParameter(parameterName, maxByteLength, precision, scale, isOutputParameter,
						            schemaName == "sys"
						                ? sqlTypeName
						                : string.Format("{0}.{1}", Tools.QuoteName(schemaName), Tools.QuoteName(sqlTypeName)), 
                                    isTableType));
						    }
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

	    public string GetCodeForTableValuedParameters()
	    {
            var tableTypes = GetTableTypes();
	        
            if (tableTypes.Count == 0)
	            return "";
            
            var code = "";

	        string nameSpace = null;
	        foreach (var tableType in tableTypes)
	        {
	            if (tableType.SchemaName != nameSpace)
	            {
                    // first namespace
	                if (string.IsNullOrWhiteSpace(nameSpace))
	                {
	                    code += "}\r";
	                }
	                nameSpace = tableType.SchemaName;
	                code += string.Format("namespace {0}.{1}.{2}\r{{\r", Config.Namespace, "TableVariables",
	                    Tools.CleanName(nameSpace));
	            }

                var columns = GetTableTypeColumns(tableType);

	            code += string.Format("public class {0}Row {{\r{1}\r}}", Tools.CleanName(tableType.Name), GetCodeForTableTypeColumns(tableType, columns));
	            code +=
	                string.Format(
	                    "public class {0} : List<{0}Row> {{public {0} (IEnumerable<{0}Row> collection) : base(collection){{}}\rinternal DataTable GetDataTable() {{{1}}}\r}}",
	                    Tools.CleanName(tableType.Name), GetCodeForDataTableConversion(tableType, columns));
	        }

	        code += "}\r";

	        return code;
	    }

	    private string GetCodeForDataTableConversion(TableType tableType, IList<TableTypeColumn> columns)
	    {
            var code = "var dt = new DataTable();\r";
	        code += string.Join("\r",
	            columns.Select(
	                column => string.Format("dt.Columns.Add(\"{0}\", typeof({1}));", column.ColumnName, column.ClrTypeName)));

            // vcreate a foreach loop

	        code += "return dt;";
	        return code;
	    }

	    private string GetCodeForTableTypeColumns(TableType tableType, IList<TableTypeColumn> columns)
	    {
            var code = string.Join("\r", columns.Select(
	            column => string.Format("public global::{0} {1} {{ get; private set; }}\r", column.ClrTypeName, Tools.CleanName(column.ColumnName))));

	        code += string.Format("public {0}({1}) {{{2}}}\r", Tools.CleanName(tableType.Name),
                 string.Join(", ", columns.Select(column=>string.Format("global::{0} {1}", column.ClrTypeName, column.ColumnName.Substring(0, 1).ToLower() + column.ColumnName.Substring(1)))),
                 string.Join("\r", columns.Select(column => string.Format("this.{0} = {1};", column.ColumnName, column.ColumnName.Substring(0, 1).ToLower() + column.ColumnName.Substring(1))))
                );
	        return code;
	    }

	    private IList<TableTypeColumn> GetTableTypeColumns(TableType tableType)
	    {
            var retValue = new List<TableTypeColumn>();

            using (var conn = new SqlConnection(DesignerConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "sp_executesql";

                    const string stmt =
                        "SELECT "+
	                        "[c].[name] AS ColumnName, "+
	                        "SCHEMA_NAME(ISNULL([st].[schema_id], [ut].[schema_id])) AS SchemaName, " +
	                        "ISNULL([st].[name], [ut].[name]) AS DataTypeName "+
                        "FROM [sys].[table_types] tt " +
	                        "INNER JOIN [sys].[columns] c "+
		                        "ON	[c].[object_id] = [tt].[type_table_object_id] "+
	                        "INNER JOIN [sys].[types] ut "+
		                        "ON	[ut].[user_type_id] = [c].[user_type_id] "+
	                        "LEFT OUTER JOIN [sys].[types] st "+
		                        "ON	[st].[user_type_id] = [c].[system_type_id]"+
                        "WHERE "+ 
	                        "[tt].[schema_id] = SCHEMA_ID(@SchemaName) "+
	                        "AND [tt].[name] = @ObjectName;";

                    cmd.Parameters.AddWithValue("@stmt", stmt);
                    cmd.Parameters.AddWithValue("@params", "@SchemaName sysname, @ObjectName sysname");
                    cmd.Parameters.AddWithValue("@SchemaName", tableType.SchemaName);
                    cmd.Parameters.AddWithValue("@ObjectName", tableType.Name);


                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var schemaName = reader.GetSqlString(1).Value;
                            var sqlTypeName = reader.GetSqlString(2).Value;
                            var clrTypeName = schemaName == "sys" ? Tools.ClrTypeName(sqlTypeName) : "System.Object";

                            retValue.Add(new TableTypeColumn(reader.GetSqlString(0).Value, clrTypeName));
                        }
                    }
                }
            }
            return retValue;
        }

	    private IList<TableType> GetTableTypes()
	    {
            var retValue = new List<TableType>();

            using (var conn = new SqlConnection(DesignerConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "sp_executesql";

                    const string stmt =
                        "SELECT SCHEMA_NAME([schema_id]) AS [SchemaName], [name] AS [ObjectName] FROM [sys].[table_types];";

                    cmd.Parameters.AddWithValue("@stmt", stmt);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            retValue.Add(new TableType(reader.GetSqlString(0).Value, reader.GetSqlString(1).Value));
                        }
                    }
                }
            }
            return retValue;
	    }
	}
}
