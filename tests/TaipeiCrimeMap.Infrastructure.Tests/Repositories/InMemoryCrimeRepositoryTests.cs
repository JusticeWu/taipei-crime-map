using FluentAssertions;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.ValueObjects;
using TaipeiCrimeMap.Infrastructure.Repositories;

namespace TaipeiCrimeMap.Infrastructure.Tests.Repositories;

public class InMemoryCrimeRepositoryTests
{
    private static TheftCase MakeCase(CaseType caseType, string district, string timeSlot, string date = "1130101") =>
        TheftCase.Create(
            caseNumber: Guid.NewGuid().ToString(),
            caseType: caseType,
            district: District.ParseFrom(district),
            occurredDate: TaiwanDate.Parse(date),
            timeSlot: TimeSlot.Parse(timeSlot),
            rawLocation: $"臺北市{district}測試路1號");

    [Fact]
    public async Task GetStatsByFilterAsync_ShouldGroupByDistrictAndTimeSlot()
    {
        // Arrange
        var repo = new InMemoryCrimeRepository();
        await repo.AddRangeAsync(new[]
        {
            MakeCase(CaseType.Residential, "大安區", "00-02"),
            MakeCase(CaseType.Residential, "大安區", "00-02"),
            MakeCase(CaseType.Residential, "信義區", "14-16"),
        });

        // Act
        var (districtCounts, timeSlotCounts) = await repo.GetStatsByFilterAsync(new CrimeFilter());

        // Assert
        districtCounts.Should().Contain(("大安區", 2));
        districtCounts.Should().Contain(("信義區", 1));
        timeSlotCounts.Should().Contain(("00~02", 2));
        timeSlotCounts.Should().Contain(("14~16", 1));
    }

    [Fact]
    public async Task GetStatsByFilterAsync_WithCaseTypeFilter_ShouldOnlyCountMatchingCases()
    {
        // Arrange
        var repo = new InMemoryCrimeRepository();
        await repo.AddRangeAsync(new[]
        {
            MakeCase(CaseType.Residential, "大安區", "00-02"),
            MakeCase(CaseType.Car, "大安區", "00-02"),
        });

        // Act
        var (districtCounts, _) = await repo.GetStatsByFilterAsync(new CrimeFilter(caseType: CaseType.Residential));

        // Assert
        districtCounts.Should().ContainSingle().Which.Should().Be(("大安區", 1));
    }

    [Fact]
    public async Task GetStatsByFilterAsync_WithNoData_ShouldReturnEmptyLists()
    {
        // Arrange
        var repo = new InMemoryCrimeRepository();

        // Act
        var (districtCounts, timeSlotCounts) = await repo.GetStatsByFilterAsync(new CrimeFilter());

        // Assert
        districtCounts.Should().BeEmpty();
        timeSlotCounts.Should().BeEmpty();
    }
}
