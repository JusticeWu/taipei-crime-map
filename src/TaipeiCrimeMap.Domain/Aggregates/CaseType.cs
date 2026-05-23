using TaipeiCrimeMap.Domain.Common;

namespace TaipeiCrimeMap.Domain.Aggregates;

public enum CaseType
{
    [ChineseName("住宅竊盜")]
    Residential = 1,

    [ChineseName("汽車竊盜")]
    Car = 2,

    [ChineseName("機車竊盜")]
    Motorcycle = 3,

    [ChineseName("自行車竊盜")]
    Bicycle = 4,

    [ChineseName("搶奪")]
    Snatching = 5,

    [ChineseName("強盜")]
    Robbery = 6,
}

public static class CaseTypeExtensions
{
    private static readonly Dictionary<string, CaseType> _fromChinese;
    private static readonly Dictionary<CaseType, string> _toChinese;

    static CaseTypeExtensions()
    {
        var mappings = BuildMappings();
        _fromChinese = mappings.ToDictionary(x => x.ChineseName, x => x.CaseType);
        _toChinese = mappings.ToDictionary(x => x.CaseType, x => x.ChineseName);
    }

    public static CaseType? FromChineseName(string raw)
    {
        if (_fromChinese.TryGetValue(raw, out var ct))
        {
            return ct;
        }

        return null;
    }

    public static string ToChineseName(this CaseType caseType)
    {
        if (_toChinese.TryGetValue(caseType, out var name))
        {
            return name;
        }

        return caseType.ToString();
    }

    private static IEnumerable<(string ChineseName, CaseType CaseType)> BuildMappings()
    {
        return Enum.GetValues<CaseType>()
            .Select(ct =>
            {
                var memberInfo = typeof(CaseType)
                    .GetMember(ct.ToString())
                    .FirstOrDefault();
                var attribute = memberInfo
                    ?.GetCustomAttributes(typeof(ChineseNameAttribute), false)
                    .FirstOrDefault() as ChineseNameAttribute;
                return (ChineseName: attribute?.Name ?? ct.ToString(), CaseType: ct);
            });
    }
}