namespace TaipeiCrimeMap.Domain.Common;

[AttributeUsage(AttributeTargets.Field)]
public class ChineseNameAttribute : Attribute
{
    public string Name { get; }

    public ChineseNameAttribute(string name)
    {
        Name = name;
    }
}
