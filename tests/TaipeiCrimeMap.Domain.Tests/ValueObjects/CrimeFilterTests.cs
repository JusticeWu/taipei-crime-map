using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Exceptions;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Tests.ValueObjects;

public class CrimeFilterTests
{
    [Fact]
    public void Constructor_WithNullCaseType_CreatesSuccessfully()
    {
        // Arrange & Act
        var filter = new CrimeFilter(caseType: null);

        // Assert
        Assert.Null(filter.CaseType);
    }

    [Fact]
    public void Constructor_WithValidCaseType_CreatesSuccessfully()
    {
        // Arrange & Act
        var filter = new CrimeFilter(caseType: CaseType.Residential);

        // Assert
        Assert.Equal(CaseType.Residential, filter.CaseType);
    }

    [Theory]
    [InlineData(2023, 2025)]    // 正常範圍
    [InlineData(2023, 2023)]    // 同年
    [InlineData(2023, null)]    // 只有 YearFrom
    [InlineData(null, 2025)]    // 只有 YearTo
    [InlineData(null, null)]    // 皆為 null
    [InlineData(2010, null)]    // YearFrom 早於資料範圍, 但合法
    [InlineData(null, 2099)]    // YearTo 晚於資料範圍, 但合法
    public void Constructor_WithValidYearRange_CreatesSuccessfully(
        int? yearFrom, int? yearTo)
    {
        // Arrange & Act
        var filter = new CrimeFilter(yearFrom: yearFrom, yearTo: yearTo);

        // Assert
        Assert.Equal(yearFrom, filter.YearFrom);
        Assert.Equal(yearTo, filter.YearTo);
    }

    [Theory]
    [InlineData(2025, 2023)]    // 倒置
    public void Constructor_WithInvalidYearRange_ThrowsDomainException(
        int? yearFrom, int? yearTo)
    {
        Assert.Throws<DomainException>(() => new CrimeFilter(yearFrom: yearFrom, yearTo: yearTo));
    }

    [Theory]
    [InlineData("18~08")]    // 倒置
    public void Constructor_WithUnparsableTimeSlot_ThrowsDomainException(string rawTimeSlot)
    {
        // Arrange & Act
        var timeSlot = TimeSlot.Parse(rawTimeSlot);

        // Assert
        Assert.Throws<DomainException>(() => new CrimeFilter(timeSlot: timeSlot));
    }
}