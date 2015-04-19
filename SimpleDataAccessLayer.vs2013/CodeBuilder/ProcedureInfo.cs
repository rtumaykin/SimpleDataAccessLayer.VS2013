namespace SimpleDataAccessLayer_vs2013.CodeBuilder
{
    public class ProcedureInfo
	{
	    public string ObjectSchemaName { get; private set; }

	    public string ObjectName { get; private set; }

	    public string FullObjectName { get { return Tools.QuoteName(ObjectSchemaName) + "." + Tools.QuoteName(ObjectName); } }

	    public string Alias { get; private set; }

	    public ProcedureInfo(string objectSchemaName, string objectName, string alias)
		{
			ObjectName = objectName;
			ObjectSchemaName = objectSchemaName;
			Alias = alias;
		}
	}
}
