using System;
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
        private readonly ISqlRepository _sqlRepository;

        public TableValuedParameter(DalConfig config, ISqlRepository sqlRepository)
        {
            _config = config;
            if (sqlRepository == null)
                throw new ArgumentNullException("sqlRepository");
            _sqlRepository = sqlRepository;
        }

        public string GetCode()
        {
            var tableTypes = _sqlRepository.GetTableTypes();

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
                            var columns = _sqlRepository.GetTableTypeColumns(ttn);
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
    }
}
