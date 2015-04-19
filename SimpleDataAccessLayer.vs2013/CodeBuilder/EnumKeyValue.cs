namespace SimpleDataAccessLayer_vs2013.CodeBuilder
{
	public class EnumKeyValue
	{
	    public string Key { get; private set; }

	    public long Value { get; private set; }

	    public EnumKeyValue(string key, long value)
		{
			Key = key;
			Value = value;
		}
	}
}
