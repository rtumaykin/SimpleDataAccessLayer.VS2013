using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace SimpleDataAccessLayer_vs2013.CodeBuilder
{

#if DEBUG 
    public class TableValuedParameter
#else 
    internal class TableValuedParameter
#endif
    {
        private readonly DalConfig _config;
        private readonly string _designerConnectionString;

        public TableValuedParameter(DalConfig config, string designerConnectionString)
        {
            _config = config;
            _designerConnectionString = designerConnectionString;
		}

        public string GetCode()
        {
            var tableTypes = GetTableTypes();

            if (tableTypes.Count == 0)
                return "";

            return string.Join("", tableTypes.Select(tt => string.Format("namespace {0}.{1}.{2} {{{3}}}",
                _config.Namespace,
                "TableVariables",
                Tools.CleanName(tt.SchemaName),
                string.Join("",
                    tableTypes.Where(ttn => ttn.SchemaName == tt.SchemaName)
                        .Select(ttn =>
                        {
                            var columns = GetTableTypeColumns(ttn);
                            return string.Format(
                                "public class {0}Row {{{1}}}" +
                                "public class {0} : global::System.Collections.Generic.List<{0}Row> {{public {0} (global::System.Collections.Generic.IEnumerable<{0}Row> collection) : base(collection){{}}internal global::System.Data.DataTable GetDataTable() {{{2}}}}}",
                                Tools.CleanName(ttn.Name),
                                GetCodeForTableTypeColumns(ttn, columns),
                                GetCodeForDataTableConversion(columns));
                        }))
                )));
        }

        private string GetCodeForDataTableConversion(IList<TableTypeColumn> columns)
        {
            var code = "var dt = new global::System.Data.DataTable();\r";
            code += string.Join("\r",
                columns.Select(
                    column => string.Format("dt.Columns.Add(\"{0}\", typeof({1}));", column.ColumnName, column.ClrTypeName)));

            code +=
                "dt.AcceptChanges();\r" +
                "foreach (var item in this) {" +
                "var row = dt.NewRow();";
            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                code += string.Format("row[{0}] = item.{1};\r", i, column.ColumnName);
            }
            code += "dt.Rows.Add(row);\r";

            code += "}\rreturn dt;";
            return code;
        }

        private string GetCodeForTableTypeColumns(TableType tableType, IList<TableTypeColumn> columns)
        {
            var code = string.Join("\r", columns.Select(
                column => string.Format("public global::{0} {1} {{ get; private set; }}\r", column.ClrTypeName, Tools.CleanName(column.ColumnName))));

            code += string.Format("public {0}Row({1}) {{{2}}}\r", Tools.CleanName(tableType.Name),
                 string.Join(", ", columns.Select(column => string.Format("global::{0} {1}", column.ClrTypeName, column.ColumnName.Substring(0, 1).ToLower() + column.ColumnName.Substring(1)))),
                 string.Join("\r", columns.Select(column => string.Format("this.{0} = {1};", column.ColumnName, column.ColumnName.Substring(0, 1).ToLower() + column.ColumnName.Substring(1))))
                );
            return code;
        }

        private IList<TableTypeColumn> GetTableTypeColumns(TableType tableType)
        {
            var retValue = new List<TableTypeColumn>();

            using (var conn = new SqlConnection(_designerConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "sp_executesql";

                    const string stmt =
                        "SELECT " +
                            "[c].[name] AS ColumnName, " +
                            "SCHEMA_NAME(ISNULL([st].[schema_id], [ut].[schema_id])) AS SchemaName, " +
                            "ISNULL([st].[name], [ut].[name]) AS DataTypeName " +
                        "FROM [sys].[table_types] tt " +
                            "INNER JOIN [sys].[columns] c " +
                                "ON	[c].[object_id] = [tt].[type_table_object_id] " +
                            "INNER JOIN [sys].[types] ut " +
                                "ON	[ut].[user_type_id] = [c].[user_type_id] " +
                            "LEFT OUTER JOIN [sys].[types] st " +
                                "ON	[st].[user_type_id] = [c].[system_type_id]" +
                        "WHERE " +
                            "[tt].[schema_id] = SCHEMA_ID(@SchemaName) " +
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

            using (var conn = new SqlConnection(_designerConnectionString))
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
