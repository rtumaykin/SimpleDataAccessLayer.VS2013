using System.Linq;

namespace SimpleDataAccessLayer_vs2013
{
	public class ProcedureParameter
	{
	    public string ParameterName { get; private set; }

	    public int MaxByteLength { get; private set; }

	    public byte Precision { get; private set; }

	    public byte Scale { get; private set; }

	    public bool IsOutputParameter { get; private set; }

	    public string SqlTypeName { get; private set; }

        public bool isTableType { get; private set; }

	    public string ClrTypeName { get { return Tools.ClrTypeName(SqlTypeName); } }

		public ProcedureParameter(string parameterName, int maxByteLength, byte precision, byte scale, bool isOutputParameter, string sqlTypeName)
		{
			ParameterName = parameterName;
			MaxByteLength = maxByteLength;
			Precision = precision;
			Scale = scale;
			IsOutputParameter = isOutputParameter;
			SqlTypeName = sqlTypeName;
		}
	}

	public class ProcedureResultSetColumn
	{
	    public string ColumnName { get; private set; }

	    public string ClrTypeName { get; private set; }

	    public ProcedureResultSetColumn(string columnName, string clrTypeName)
		{
			ColumnName = columnName;
			if ("System.Int64 System.Boolean System.DateTime System.DateTimeOffset System.Decimal System.Double Microsoft.SqlServer.Types.SqlHierarchyId System.Int32 System.Single System.Int16 System.TimeSpan System.Byte System.Guid".Split(' ').Contains(clrTypeName))
			{
				clrTypeName += "?";
			}
			ClrTypeName = clrTypeName;
		}
	}

	public class ProcedureDescriptor
	{
	    public string ObjectSchemaName { get; private set; }

	    public string ObjectName { get; private set; }

	    public string FullObjectName { get { return Tools.QuoteName(ObjectSchemaName) + "." + Tools.QuoteName(ObjectName); } }

	    public string Alias { get; private set; }

	    public ProcedureDescriptor(string objectSchemaName, string objectName, string alias)
		{
			ObjectName = objectName;
			ObjectSchemaName = objectSchemaName;
			Alias = alias;
		}
	}
}
