namespace SimpleDataAccessLayer.vs2013.TransformationHelper
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
