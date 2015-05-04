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
#if DEBUG 
    public class Procedure
#else 
    internal class Procedure
#endif
    {

        private readonly DalConfig _config;
        private readonly ISqlRepository _sqlRepository;

        public Procedure(DalConfig config, ISqlRepository sqlRepository)
        {
            _config = config;
            if (sqlRepository == null)
                throw new ArgumentNullException("sqlRepository");
            _sqlRepository = sqlRepository;
		}

        public string GetCode()
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
                                Tools.ValidIdentifier(string.IsNullOrWhiteSpace(proc.Alias) ? proc.ProcedureName : proc.Alias), 
                                GetProcedureBodyCode(proc))));
        }

        private string GetProcedureBodyCode(SimpleDataAccessLayer_vs2013.Procedure proc)
        {
            var parameters = _sqlRepository.GetProcedureParameterCollection(proc.Schema, proc.ProcedureName);
            var recordsets = _sqlRepository.GetProcedureResultSetColumnCollection(proc.Schema, proc.ProcedureName);

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
                        string.Format(
                            "public static {0}{1}{2} Execute{3} ({4} global::{5}.ExecutionScope executionScope = null, global::System.Int32 commandTimeout = 30){{{6}}}/*end*/",
                            async ? "async global::System.Threading.Tasks.Task<" : "",
                            Tools.ValidIdentifier(proc.ProcedureName),
                            async ? ">" : "",
                            async ? "Async" : "",
                            string.Join("",
                                parameters.Select(
                                    parameter =>
                                        string.Format("global::{0} {1},",
                                            parameter.IsTableType
                                                ? string.Format("{0}.{1}", _config.Namespace, parameter.ClrTypeName)
                                                : parameter.ClrTypeName, parameter.AsLocalVariableName))),
                            _config.Namespace,
                            GetExecuteBodyCode(async, parameters, recordsets, proc)
                            )));
        }

        private string GetExecuteBodyCode(bool async, IList<ProcedureParameter> parameters, IList<List<ProcedureResultSetColumn>> recordsets, SimpleDataAccessLayer_vs2013.Procedure proc)
        {
            var code =
                string.Format("var retValue = new {0}();", Tools.ValidIdentifier(string.IsNullOrWhiteSpace(proc.Alias) ? proc.ProcedureName : proc.Alias)) +
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
                "	using (global::System.Data.SqlClient.SqlCommand cmd = conn.CreateCommand()) {" +
                "			cmd.CommandType = global::System.Data.CommandType.StoredProcedure;" +
                "			if (executionScope != null && executionScope.Transaction != null)" +
                "				cmd.Transaction = executionScope.Transaction;" +
                "               cmd.CommandTimeout = commandTimeout;" +  
                string.Format("			cmd.CommandText = \"{0}.{1}\";", Tools.QuoteName(proc.Schema),
                    Tools.QuoteName(proc.ProcedureName)) +
                string.Join("", parameters.Select(p => p.IsTableType
                    ? string.Format(
                        "cmd.Parameters.Add(new global::System.Data.SqlClient.SqlParameter(\"@{0}\", global::System.Data.SqlDbType.Structured) {{TypeName = \"{1}\", Value = {2}.GetDataTable()}});",
                        p.ParameterName, p.SqlTypeName, p.AsLocalVariableName)
                    : string.Format(
                        "cmd.Parameters.Add(new global::System.Data.SqlClient.SqlParameter(\"@{0}\", global::System.Data.SqlDbType.{1}, {2}, global::System.Data.ParameterDirection.{3}, true, {4}, {5}, null, global::System.Data.DataRowVersion.Default, {6}){{{7}}});",
                        p.ParameterName,
                        Tools.SqlDbTypeName(p.SqlTypeName),
                        ("nchar nvarchar".Split(' ').Contains(p.SqlTypeName) && p.MaxByteLength != -1)
                            ? (p.MaxByteLength/2)
                            : p.MaxByteLength,
                        p.IsOutputParameter ? "Output" : "Input",
                        p.Precision,
                        p.Scale,
                        p.AsLocalVariableName,
                        "geography hierarchyid geometry".Split(' ').Contains(p.SqlTypeName)
                            ? string.Format("UdtTypeName = \"{0}\"", p.SqlTypeName)
                            : ""
                        ))) +
                "cmd.Parameters.Add(new global::System.Data.SqlClient.SqlParameter(\"@ReturnValue\", global::System.Data.SqlDbType.Int, 4, global::System.Data.ParameterDirection.ReturnValue, true, 0, 0, null, global::System.Data.DataRowVersion.Default, global::System.DBNull.Value));" +
                (recordsets.Count == 0
                    ? string.Format("{0} cmd.ExecuteNonQuery{1}();",
                        async ? "await" : "",
                        async ? "Async" : "")
                    : string.Format(
                        "using (global::System.Data.SqlClient.SqlDataReader reader = {0} cmd.ExecuteReader{1}()){{{2}}}",
                        async ? "await" : "",
                        async ? "Async" : "",
                        MapRecordsetResults(async, recordsets)
                        )) +
                string.Format("retValue.Parameters = new ParametersCollection({0});", string.Join(
                    ", ",
                    parameters.Select(
                        parameter =>
                            // only copy from these the output parameters. Everything else shoud just be coming from the input params
                            parameter.IsOutputParameter ?
                            string.Format(
                                "cmd.Parameters[\"@{0}\"].Value == global::System.DBNull.Value ? null : (global::{1}) cmd.Parameters[\"@{0}\"].Value",
                                parameter.ParameterName,
                                string.Format("{0}{1}", parameter.IsTableType ? _config.Namespace + "." : "", parameter.ClrTypeName)):
                                parameter.AsLocalVariableName
                    ))) +
                "retValue.ReturnValue = (global::System.Int32) cmd.Parameters[\"@ReturnValue\"].Value; " +
                "return retValue;" +
                "}" +
                "}" +
                "catch (global::System.Data.SqlClient.SqlException e) {" +
                "if (retryCycle++ > 9 || !ExecutionScope.RetryableErrors.Contains(e.Number))" +
                "   throw;" +
                "global::System.Threading.Thread.Sleep(1000);" +
                "}" +
                "finally {" +
                "if (executionScope == null &&  conn != null) {" +
                "((global::System.IDisposable) conn).Dispose();" +
                "}" +
                "}" +
                "}" +
//                "}" +
                "}"
                ;

            return code;

        }

        private string MapRecordsetResults(bool @async, IList<List<ProcedureResultSetColumn>> recordsets)
        {
            var code = "";
            for (var rsNo = 0; rsNo < recordsets.Count; rsNo++)
            {
                var recordset = recordsets[rsNo];
                var internalCode = string.Format("retValue.Recordset{0} = new global::System.Collections.Generic.List<Record{0}>(); while ({1} reader.Read{2}()) {{{3}}}",
                    rsNo,
                    async ? "await" : "",
                    async ? "Async" : "",
                    string.Format("retValue.Recordset{0}.Add(new Record{0}({1}));",
                        rsNo,
                        MapRecord(async, recordset))
                    );
                if (rsNo > 0)
                {
                    internalCode = string.Format("if ({0} reader.NextResult{1}()){{{2}}}",
                        async ? "await" : "",
                        async ? "Async" : "",
                        internalCode);
                }

                code += internalCode;
            }


            return code;
        }

        private string MapRecord(bool async, List<ProcedureResultSetColumn> recordset)
        {
            var code = "";

            for (var colNo = 0; colNo < recordset.Count; colNo ++)
            {
                var column = recordset[colNo];
                code += string.Format("{4} reader.IsDBNull({0}) ? null : {1} reader.GetFieldValue{2}<global::{3}>({0})",
                    colNo,
                    async ? "await" : "",
                    async ? "Async" : "",
                    column.ClrTypeName,
                    colNo > 0 ? "," : ""
                    );
            }

            return code;
        }

        private string GetRecordsetsDefinitions(IList<List<ProcedureResultSetColumn>> recordsets)
        {
            var code = "";
            
            for (var rsNo = 0; rsNo < recordsets.Count; rsNo++)
            {
                var recordset = recordsets[rsNo];

                code += string.Format("public class Record{0} {{{1}public Record{0}({2}){{{3}}}}}public global::System.Collections.Generic.List<Record{0}> Recordset{0}{{get;private set;}}",
                    rsNo,
                    string.Join("",
                        recordset.Select(
                            column =>
                                string.Format("public global::{0} {1} {{get; private set;}}", column.ClrTypeName,
                                    Tools.ValidIdentifier(column.ColumnName)))),
                    string.Join(",",
                        recordset.Select(
                            column =>
                                string.Format("global::{0} {1}", column.ClrTypeName,
                                    Tools.ValidIdentifier(column.ColumnName).Substring(0, 1).ToLowerInvariant() +
                                    Tools.ValidIdentifier(column.ColumnName).Substring(1)))),
                    string.Join("",
                        recordset.Select(
                            column =>
                                string.Format("this.{0} = {1};", column.ColumnName,
                                    Tools.ValidIdentifier(column.ColumnName).Substring(0, 1).ToLowerInvariant() +
                                    Tools.ValidIdentifier(column.ColumnName).Substring(1))))
                    );
            }

            return code;
        }

        private string GetParameterCollectionCode(IList<ProcedureParameter> parameters)
        {
            return
                string.Format(
                    "public class ParametersCollection {{{0}public ParametersCollection({1}){{{2}}}}}public ParametersCollection Parameters {{get;private set;}}",
                    string.Join("", parameters.Select(
                        p => 
                            string.Format("public global::{0} {1} {{get;private set;}}",
                                string.Format("{0}{1}", p.IsTableType ? _config.Namespace + "." : "", p.ClrTypeName), // for Clr type need to add current namespace
                                p.ParameterName))),
                    string.Join(",", parameters.Select(
                        p =>
                            string.Format("global::{0} {1}", string.Format("{0}{1}", p.IsTableType ? _config.Namespace + "." : "", p.ClrTypeName),
                                p.AsLocalVariableName))),
                    string.Join("", parameters.Select(
                        p =>
                            string.Format("this.{0} = {1};", p.ParameterName,
                                p.AsLocalVariableName)))
                    );
        }
    }
}
