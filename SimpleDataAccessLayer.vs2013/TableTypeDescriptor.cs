using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleDataAccessLayer_vs2013
{
    public class TableTypeColumn
    {
        public string ColumnName { get; private set; }

        public string ClrTypeName { get; private set; }

        public TableTypeColumn(string columnName, string clrTypeName)
        {
            ColumnName = columnName;
            if ("System.Int64 System.Boolean System.DateTime System.DateTimeOffset System.Decimal System.Double Microsoft.SqlServer.Types.SqlHierarchyId System.Int32 System.Single System.Int16 System.TimeSpan System.Byte System.Guid".Split(' ').Contains(clrTypeName))
            {
                clrTypeName += "?";
            }
            ClrTypeName = clrTypeName;
        }
    }

    public class TableType
    {
        public string SchemaName { get; private set; }
        public string Name { get; private set; }

        public TableType(string schemaName, string name)
        {
            SchemaName = schemaName;
            Name = name;
        }
    }
}
