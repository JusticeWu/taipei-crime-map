using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Tests.Aggregates;

public class TheftCaseTests
{
    [Fact]
    public void IsDataComplete_WithValidInputs_ReturnsTrue()
    {
        var theftCase = TheftCase.Create(
            caseNumber: "A123",
            caseType: CaseType.Residential,
            district: District.ParseFrom("台北市內湖區成功路五段31號")!,
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: TimeSlot.Parse("08~18"),
            coordinate: GeoCoordinate.Create(25.04776, 121.51737));

        Assert.True(theftCase.IsDataComplete);
    }

    [Fact]
    public void IsDataComplete_WithNullDistrict_ReturnsFalse()
    {
        var theftCase = TheftCase.Create(
            caseNumber: "A123",
            caseType: CaseType.Residential,
            district: null,
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: TimeSlot.Parse("08~18"),
            coordinate: GeoCoordinate.Create(25.04776, 121.51737));

        Assert.False(theftCase.IsDataComplete);
    }

    
}
