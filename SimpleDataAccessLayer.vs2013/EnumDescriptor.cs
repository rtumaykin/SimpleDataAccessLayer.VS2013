namespace SimpleDataAccessLayer_vs2013
{
	public class EnumData
	{
	    public string Key { get; private set; }

	    public long Value { get; private set; }

	    public EnumData(string key, long value)
		{
			Key = key;
			Value = value;
		}
	}
}
