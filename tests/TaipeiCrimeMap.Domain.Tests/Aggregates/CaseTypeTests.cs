using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.Domain.Tests.Aggregates;

public class CaseTypeTests
{
    [Theory]
    [InlineData("住宅竊盜", CaseType.Residential)]
    [InlineData("汽車竊盜", CaseType.Car)]
    [InlineData("機車竊盜", CaseType.Motorcycle)]
    [InlineData("自行車竊盜", CaseType.Bicycle)]
    [InlineData("搶奪", CaseType.Snatching)]
    [InlineData("強盜", CaseType.Robbery)]
    public void FromChineseName_ValidName_ReturnsCaseType(string chinese, CaseType expected)
    {
        Assert.Equal(expected, CaseTypeExtensions.FromChineseName(chinese));
    }

    [Theory]
    [InlineData("")]
    [InlineData("不存在的案類")]
    [InlineData("竊盜")]
    public void FromChineseName_InvalidName_ReturnsNull(string chinese)
    {
        Assert.Null(CaseTypeExtensions.FromChineseName(chinese));
    }

    [Theory]
    [InlineData(CaseType.Residential, "住宅竊盜")]
    [InlineData(CaseType.Car, "汽車竊盜")]
    [InlineData(CaseType.Motorcycle, "機車竊盜")]
    [InlineData(CaseType.Bicycle, "自行車竊盜")]
    [InlineData(CaseType.Snatching, "搶奪")]
    [InlineData(CaseType.Robbery, "強盜")]
    public void ToChineseName_ValidEnum_ReturnsChineseName(CaseType caseType, string expected)
    {
        Assert.Equal(expected, caseType.ToChineseName());
    }
}
