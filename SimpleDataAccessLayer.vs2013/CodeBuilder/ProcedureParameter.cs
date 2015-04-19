namespace SimpleDataAccessLayer_vs2013.CodeBuilder
{
    public class ProcedureParameter
    {
        public string ParameterName { get; private set; }

        public int MaxByteLength { get; private set; }

        public byte Precision { get; private set; }

        public byte Scale { get; private set; }

        public bool IsOutputParameter { get; private set; }

        public string SqlTypeName { get; private set; }

        public bool IsTableType { get; private set; }

        public string ClrTypeName
        {
            get
            {
                // if this is a table type, then the CLR Type name must already be defined. It all should follow the same rules as any other object. 
                // I.e "types", then schema name as a part of the namespace, then class name equals to the type name 
                return IsTableType ? "System.Object" : Tools.ClrTypeName(SqlTypeName);
	            
            }
        }

        public ProcedureParameter(string parameterName, int maxByteLength, byte precision, byte scale, bool isOutputParameter, string sqlTypeName, bool isTableType)
        {
            ParameterName = parameterName;
            MaxByteLength = maxByteLength;
            Precision = precision;
            Scale = scale;
            IsOutputParameter = isOutputParameter;
            SqlTypeName = sqlTypeName;
            IsTableType = isTableType;
        }
    }
}