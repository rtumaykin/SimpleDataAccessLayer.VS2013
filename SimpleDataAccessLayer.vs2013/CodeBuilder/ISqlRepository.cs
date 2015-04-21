using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleDataAccessLayer_vs2013.CodeBuilder
{
#if DEBUG 
    public interface ISqlRepository
#else 
    internal interface ISqlRepository
#endif
    {
        IList<EnumKeyValue> GetEnumKeyValues(string objectSchemaName, string objectName, string valueColumnName,
            string keyColumnName);

        IList<ProcedureParameter> GetProcedureParameterCollection(string objectSchemaName, string objectName);

        IList<List<ProcedureResultSetColumn>> GetProcedureResultSetColumnCollection(string objectSchemaName,
            string objectName);

        IList<TableTypeColumn> GetTableTypeColumns(TableType tableType);

        IList<TableType> GetTableTypes();
    }
}
