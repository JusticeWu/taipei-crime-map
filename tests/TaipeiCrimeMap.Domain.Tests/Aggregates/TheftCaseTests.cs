using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Exceptions;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Tests.Aggregates;

public class TheftCaseTests
{
    private const string ValidLocation = "台北市內湖區成功路五段31號";


    [Fact]
    public void IsDataComplete_WithValidInputs_ReturnsTrue()
    {
        var theftCase = TheftCase.Create(
            caseNumber: 123,
            caseType: CaseType.Residential,
            district: District.ParseFrom(ValidLocation)!,
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: TimeSlot.Parse("08~18"),
            rawLocation: ValidLocation,
            coordinate: GeoCoordinate.Create(25.04776, 121.51737));

        Assert.True(theftCase.IsDataComplete);
    }

    [Fact]
    public void IsDataComplete_WithNullDistrict_ReturnsFalse()
    {
        var theftCase = TheftCase.Create(
            caseNumber: 123,
            caseType: CaseType.Residential,
            district: null,
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: TimeSlot.Parse("08~18"),
            rawLocation: ValidLocation,
            coordinate: GeoCoordinate.Create(25.04776, 121.51737));

        Assert.False(theftCase.IsDataComplete);
    }

    [Fact]
    public void IsDataComplete_WithInvalidOccurredDate_ReturnsFalse()
    {
        var theftCase = TheftCase.Create(
            caseNumber: 123,
            caseType: CaseType.Residential,
            district: District.ParseFrom(ValidLocation)!,
            occurredDate: TaiwanDate.Parse("invalid"),
            timeSlot: TimeSlot.Parse("08~18"),
            rawLocation: ValidLocation,
            coordinate: GeoCoordinate.Create(25.04776, 121.51737));

        Assert.False(theftCase.IsDataComplete);
    }

    [Fact]
    public void IsDataComplete_WithInvalidTimeSlot_ReturnsFalse()
    {
        var theftCase = TheftCase.Create(
            caseNumber: 123,
            caseType: CaseType.Residential,
            district: District.ParseFrom(ValidLocation)!,
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: TimeSlot.Parse("18~08"),  // 倒置
            rawLocation: ValidLocation,
            coordinate: GeoCoordinate.Create(25.04776, 121.51737));

        Assert.False(theftCase.IsDataComplete);
    }

    [Fact]
    public void IsDataComplete_WithNullCaseType_ReturnsFalse()
    {
        var theftCase = TheftCase.Create(
            caseNumber: 123,
            caseType: null,
            district: District.ParseFrom(ValidLocation)!,
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: TimeSlot.Parse("08~18"),
            rawLocation: ValidLocation,
            coordinate: GeoCoordinate.Create(25.04776, 121.51737));

        Assert.False(theftCase.IsDataComplete);
    }

    [Fact]
    public void Create_WithNullOccurredDate_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            TheftCase.Create(
                caseNumber: 123,
                caseType: CaseType.Residential,
                district: District.ParseFrom(ValidLocation)!,
                occurredDate: null!,
                timeSlot: TimeSlot.Parse("08~18"),
                rawLocation: ValidLocation,
                coordinate: GeoCoordinate.Create(25.04776, 121.51737)));
    }

    [Fact]
    public void Create_WithNullRawLocation_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            TheftCase.Create(
                caseNumber: 123,
                caseType: CaseType.Residential,
                district: District.ParseFrom(ValidLocation),
                occurredDate: TaiwanDate.Parse("1130101"),
                timeSlot: TimeSlot.Parse("08~18"),
                rawLocation: null!,
                coordinate: GeoCoordinate.Create(25.04776, 121.51737)));
    }
}
