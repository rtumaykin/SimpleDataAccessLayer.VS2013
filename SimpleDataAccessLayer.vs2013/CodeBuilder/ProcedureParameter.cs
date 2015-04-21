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

        public string ClrTypeName { get; private set; }

        public ProcedureParameter(string parameterName, int maxByteLength, byte precision, byte scale, bool isOutputParameter, string sqlTypeName, bool isTableType, string clrTypeName)
        {
            ParameterName = parameterName;
            MaxByteLength = maxByteLength;
            Precision = precision;
            Scale = scale;
            IsOutputParameter = isOutputParameter;
            SqlTypeName = sqlTypeName;
            IsTableType = isTableType;
            ClrTypeName = clrTypeName;
        }

        public string AsLocalVariableName
        {
            get { return Tools.CleanName(ParameterName.Substring(0, 1).ToLowerInvariant() + ParameterName.Substring(1)); }
        }
    }
}