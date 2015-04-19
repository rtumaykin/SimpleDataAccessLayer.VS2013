using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.Management.Common;

namespace SimpleDataAccessLayer_vs2013.CodeBuilder
{
    internal class Procedure
    {

                private readonly DalConfig _config;
        private readonly string _designerConnectionString;

        public Procedure(DalConfig config, string designerConnectionString)
        {
            _config = config;
            _designerConnectionString = designerConnectionString;
		}

        private IList<ProcedureParameter> GetProcedureParameterCollection(string objectSchemaName, string objectName)
        {
            var fullObjectName = Tools.QuoteName(objectSchemaName) + "." + Tools.QuoteName(objectName);
            var retValue = new List<ProcedureParameter>();

            using (var conn = new SqlConnection(_designerConnectionString))
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

                            var clrTypeName = schemaName == "sys"
                                ? Tools.ClrTypeName(sqlTypeName)
                                : string.Format("{0}.TableVariables.{1}.{2}", _config.Namespace, Tools.CleanName(schemaName), Tools.CleanName(sqlTypeName));

                            if (!string.IsNullOrWhiteSpace(clrTypeName) || isTableType)
                            {
                                retValue.Add(new ProcedureParameter(parameterName, maxByteLength, precision, scale,
                                    isOutputParameter,
                                    schemaName == "sys"
                                        ? sqlTypeName
                                        : string.Format("{0}.{1}", Tools.QuoteName(schemaName),
                                            Tools.QuoteName(sqlTypeName)),
                                    isTableType));
                            }
                        }
                    }
                }
            }
            return retValue;
        }

        private byte GetDatabaseCompatibilityLevel()
        {
            byte retValue;
            using (var conn = new SqlConnection(_designerConnectionString))
            {
                conn.Open();

                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = "sp_executesql";
                        cmd.Parameters.AddWithValue("@stmt",
                            @"SELECT @CompatibilityLevel = [compatibility_level] FROM sys.[databases] WHERE [database_id] = DB_ID();");
                        cmd.Parameters.AddWithValue("@params", "@CompatibilityLevel tinyint OUTPUT");
                        cmd.Parameters.Add(new SqlParameter("@CompatibilityLevel", SqlDbType.TinyInt)
                        {
                            Direction = ParameterDirection.Output
                        });
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

        private IList<List<ProcedureResultSetColumn>> GetProcedureResultSetColumnCollection(string objectSchemaName,
            string objectName)
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

        private IList<List<ProcedureResultSetColumn>> GetProcedureResultSetColumnCollection2005(string objectSchemaName,
            string objectName)
        {
            var fullObjectName = Tools.QuoteName(objectSchemaName) + "." + Tools.QuoteName(objectName);
            var retValue = new List<List<ProcedureResultSetColumn>>();
            try
            {
                var sb = new SqlConnectionStringBuilder(_designerConnectionString);
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

                var ds = conn.ExecuteWithResults(new StringCollection() { "SET FMTONLY ON;", procedureCall + ";" });
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

        private IList<List<ProcedureResultSetColumn>> GetProcedureResultSetColumnCollection2014(string objectSchemaName,
            string objectName)
        {
            var firstRecordset = new List<ProcedureResultSetColumn>();
            var fullObjectName = Tools.QuoteName(objectSchemaName) + "." + Tools.QuoteName(objectName);

            using (var conn = new SqlConnection(_designerConnectionString))
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
                            firstRecordset.Add(new ProcedureResultSetColumn(reader.GetSqlString(0).Value,
                                Tools.ClrTypeName(reader.GetSqlString(3).Value)));

                        }
                    }
                }
            }

            return new List<List<ProcedureResultSetColumn>> { firstRecordset };
        }

        internal string GetCode()
        {
            if (_config == null || _config.Procedures == null)
                return "";

            return string.Join("", _config.Procedures.Select(e => e.Schema)
                .Distinct().Select(ns =>
                    string.Format("namespace {0}.Executables.{1} {{{2}}}", _config.Namespace, ns,
                        GetProceduresCodeForNamespace(ns))));
        }

        private string GetProceduresCodeForNamespace(string ns)
        {
            return string.Join("",
                _config.Procedures.Where(e => e.Schema == ns)
                    .Select(
                        proc =>
                            string.Format("public class {0}{{{1}}}",
                                Tools.CleanName(proc.Alias ?? proc.ProcedureName), GetProcedureBodyCode(proc))));
        }

        private string GetProcedureBodyCode(SimpleDataAccessLayer_vs2013.Procedure proc)
        {
            var parameters = GetProcedureParameterCollection(proc.Schema, proc.ProcedureName);
            var recordsets = GetProcedureResultSetColumnCollection(proc.Schema, proc.ProcedureName);

            return string.Format("{0}public global::System.Int32 ReturnValue {{get; private set;}}{1}{2}",
                GetParameterCollectionCode(parameters),
                GetRecordsetsDefinitions(recordsets),
                GetExecuteCode(proc, parameters, recordsets));
        }

        private string GetExecuteCode(SimpleDataAccessLayer_vs2013.Procedure proc, IList<ProcedureParameter> parameters, IList<List<ProcedureResultSetColumn>> recordsets)
        {
            return string.Join("",
                new[] {true, false}.Select(
                    async =>
                        string.Format("public static {0}{1}{2} Execute{3} ({4}, {5}.ExecutionScope scope = null){{{6}}}",
                            async ? "async global::System.Threading.Tasks.Task<" : "",
                            Tools.CleanName(proc.ProcedureName),
                            async ? ">" : "",
                            async ? "Async" : "",
                            string.Join(",",
                                parameters.Select(
                                    parameter =>
                                        string.Format("{0} {1}", parameter.ClrTypeName, parameter.ParameterName))),
                            _config.Namespace,
                            GetExecuteBodyCode(async, parameters, recordsets, proc)
                            )));
        }

        private string GetExecuteBodyCode(bool async, IList<ProcedureParameter> parameters, IList<List<ProcedureResultSetColumn>> recordsets, SimpleDataAccessLayer_vs2013.Procedure proc)
        {
            var code =
                string.Format("var retValue = new {0}();", proc.Alias ?? proc.ProcedureName) +
                "{" +
                "   var retryCycle = 0;" +
                "   while (true) {" +
                string.Format(
                    "       global::System.Data.SqlClient.SqlConnection conn = executionScope == null ? new global::System.Data.SqlClient.SqlConnection(global::{0}.ExecutionScope.ConnectionString) : executionScope.Transaction.Connection;",
                    _config.Namespace) +
                "       try {" +
                "           if (conn.State != global::System.Data.ConnectionState.Open) {" +
                "               if (executionScope == null) {" +
                string.Format("				{0} conn.Open{1}();", async ? "await" : "", async ? "Async" : "") +
                "			}" +
                "			else {" +
                "				retryCycle = int.MaxValue; " +
                "				throw new global::System.Exception(\"Execution Scope must have an open connection.\"); " +
                "			}" +
                "		}" +
                "	using (global::System.Data.SqlClient.SqlCommand cmd = _conn.CreateCommand()) {" +
                "			cmd.CommandType = global::System.Data.CommandType.StoredProcedure;" +
                "			if (executionScope != null && executionScope.Transaction != null)" +
                "				cmd.Transaction = executionScope.Transaction;" +
                string.Format("			cmd.CommandText = \"{0}.{1}\";", Tools.QuoteName(proc.Schema),
                    Tools.QuoteName(proc.ProcedureName)) +
                string.Join("", parameters.Select(p => p.IsTableType
                    ? string.Format(
                        "cmd.Parameters.Add(new global::System.Data.SqlClient.SqlParameter(\"@{0}\", global::System.Data.SqlDbType.Structured) {{TypeName = \"{1}\", Value = {2}.GetDataTable()}}",
                        p.ParameterName, p.SqlTypeName, p.ParameterName.Substring(0, 1) + p.ParameterName.Substring(1)) :

                    string.Format("cmd.Parameters.Add(new global::System.Data.SqlClient.SqlParameter(\"@{0}\", global::System.Data.SqlDbType.{1}, {2}, {3}, true, {4}, {5}, null, global::System.Data.DataRowVersion.Default, {6}){{{7}}}",
                        p.ParameterName,
                        Tools.SqlDbTypeName(p.SqlTypeName),
                        ("nchar nvarchar".Split(' ').Contains(p.SqlTypeName) && p.MaxByteLength != -1) ? (p.MaxByteLength / 2) : p.MaxByteLength,
                        p.IsOutputParameter ? "Output" : "Input",
                        p.Precision,
                        p.Scale,
                        p.ParameterName.Substring(0, 1) + p.ParameterName.Substring(1),
                        "geography hierarchyid geometry".Split(' ').Contains(p.SqlTypeName) ? string.Format("UdtTypeName = \"{0}\"", p.SqlTypeName) : ""
                    ))) +
                    "cmd.Parameters.Add(new global::System.Data.SqlClient.SqlParameter(\"@ReturnValue\", global::System.Data.SqlDbType.Int, 4, global::System.Data.ParameterDirection.ReturnValue, true, 0, 0, null, global::System.Data.DataRowVersion.Default, global::System.DBNull.Value));" +
                    (recordsets.Count > 0 ? 
                        string.Format("{0} cmd.ExecuteNonQuery{1}();",
                            async ? "await" : "",
                            async ? "Async" : "") : 
                        "")
                ;

            return code;

        }

        private string GetRecordsetsDefinitions(IList<List<ProcedureResultSetColumn>> recordsets)
        {
            var code = "";
            
            for (var rsNo = 0; rsNo < recordsets.Count; rsNo++)
            {
                var recordset = recordsets[rsNo];

                code += string.Format("public class Record{0} {{{1}public Record{0}({2}){{{3}}}}}public global::System.Collections.Generic.List<Record{0}>{{get;private set;}}",
                    rsNo,
                    string.Join("",
                        recordset.Select(
                            column =>
                                string.Format("public global::{0} {1} {{get; private set;}}", column.ClrTypeName,
                                    Tools.CleanName(column.ColumnName)))),
                    string.Join("",
                        recordset.Select(
                            column =>
                                string.Format("public global::{0} {1}", column.ClrTypeName,
                                    Tools.CleanName(column.ColumnName).Substring(0, 1).ToLowerInvariant() +
                                    Tools.CleanName(column.ColumnName).Substring(1)))),
                    string.Join("",
                        recordset.Select(
                            column =>
                                string.Format("this.{0} = {1};", column.ColumnName,
                                    Tools.CleanName(column.ColumnName).Substring(0, 1).ToLowerInvariant() +
                                    Tools.CleanName(column.ColumnName).Substring(1))))
                    );
            }

            return code;
        }

        private string GetParameterCollectionCode(IList<ProcedureParameter> parameters)
        {
            return
                string.Format(
                    "public class ParametersCollection {{{0}}}public ParametersCollection Parameters {{get;private set;}}",
                    parameters.Select(
                        p => string.Format("global::{0} {1} {{get;private set;}}", p.ClrTypeName, p.ParameterName)));
        }
    }
}
