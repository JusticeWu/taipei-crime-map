using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Services;

namespace TaipeiCrimeMap.Domain.Tests.Services;

public class AnalysisGroupingTests
{
    [Theory]
    [InlineData("中正區", "G1")]
    [InlineData("大同區", "G1")]
    [InlineData("萬華區", "G1")]
    [InlineData("松山區", "G2")]
    [InlineData("信義區", "G2")]
    [InlineData("大安區", "G2")]
    [InlineData("中山區", "G2")]
    [InlineData("士林區", "G3")]
    [InlineData("北投區", "G3")]
    [InlineData("內湖區", "G4")]
    [InlineData("南港區", "G4")]
    [InlineData("文山區", "G5")]
    public void GetDistrictGroup_AllDistricts_ReturnCorrectGroup(string district, string expected)
    {
        Assert.Equal(expected, AnalysisGrouping.GetDistrictGroup(district));
    }

    [Theory]
    [InlineData(CaseType.Residential, "C1")]
    [InlineData(CaseType.Car, "C2")]
    [InlineData(CaseType.Motorcycle, "C2")]
    [InlineData(CaseType.Bicycle, "C2")]
    [InlineData(CaseType.Snatching, "C3")]
    [InlineData(CaseType.Robbery, "C3")]
    public void GetCaseTypeGroup_AllCaseTypes_ReturnCorrectGroup(CaseType caseType, string expected)
    {
        Assert.Equal(expected, AnalysisGrouping.GetCaseTypeGroup(caseType));
    }

    [Theory]
    [InlineData(0, "T1")]
    [InlineData(3, "T1")]
    [InlineData(4, "T2")]
    [InlineData(7, "T2")]
    [InlineData(8, "T3")]
    [InlineData(11, "T3")]
    [InlineData(12, "T4")]
    [InlineData(15, "T4")]
    [InlineData(16, "T5")]
    [InlineData(18, "T5")]
    [InlineData(19, "T5")]
    [InlineData(20, "T6")]
    [InlineData(23, "T6")]
    public void GetTimeSlotGroup_AllBoundaries_ReturnCorrectGroup(int startHour, string expected)
    {
        Assert.Equal(expected, AnalysisGrouping.GetTimeSlotGroup(startHour));
    }

    [Fact]
    public void GetDistrictsInGroup_G2_ReturnsFourDistricts()
    {
        var districts = AnalysisGrouping.GetDistrictsInGroup("G2");
        Assert.Equal(4, districts.Count);
        Assert.Contains("信義區", districts);
        Assert.Contains("大安區", districts);
    }

    [Fact]
    public void GetCaseTypesInGroup_C2_ReturnsVehicleThefts()
    {
        var types = AnalysisGrouping.GetCaseTypesInGroup("C2");
        Assert.Equal(3, types.Count);
        Assert.Contains((int)CaseType.Car, types);
        Assert.Contains((int)CaseType.Motorcycle, types);
        Assert.Contains((int)CaseType.Bicycle, types);
    }

    [Fact]
    public void GetTimeSlotRange_T5_Returns16To20()
    {
        var (min, max) = AnalysisGrouping.GetTimeSlotRange("T5");
        Assert.Equal(16, min);
        Assert.Equal(20, max);
    }
}
