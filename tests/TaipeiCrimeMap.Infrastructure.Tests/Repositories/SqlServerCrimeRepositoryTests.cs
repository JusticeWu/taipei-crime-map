using FluentAssertions;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;
using TaipeiCrimeMap.Infrastructure.Repositories;

namespace TaipeiCrimeMap.Infrastructure.Tests.Repositories;

public class SqlServerCrimeRepositoryTests
{
    [Fact]
    public async Task GetStatsByFilterAsync_ReturnsBothDistrictAndTimeSlotCounts()
    {
        ICrimeRepository repository = new InMemoryCrimeRepository();

        var case1 = TheftCase.Create(
            caseNumber: "T001",
            caseType: CaseType.Residential,
            district: District.ParseFrom("臺北市大安區"),
            occurredDate: TaiwanDate.Parse("1150329"),
            timeSlot: TimeSlot.Parse("06~08"),
            rawLocation: "臺北市大安區忠孝東路");

        var case2 = TheftCase.Create(
            caseNumber: "T002",
            caseType: CaseType.Car,
            district: District.ParseFrom("臺北市中山區"),
            occurredDate: TaiwanDate.Parse("1150401"),
            timeSlot: TimeSlot.Parse("10~12"),
            rawLocation: "臺北市中山區南京東路");

        await repository.AddAsync(case1);
        await repository.AddAsync(case2);

        var (districtCounts, timeSlotCounts) = await repository.GetStatsByFilterAsync(new CrimeFilter());

        districtCounts.Should().HaveCount(2);
        districtCounts.Should().Contain(d => d.District == "大安區");
        districtCounts.Should().Contain(d => d.District == "中山區");

        timeSlotCounts.Should().HaveCount(2);
        timeSlotCounts.Should().Contain(t => t.TimeSlot == "06~08");
        timeSlotCounts.Should().Contain(t => t.TimeSlot == "10~12");
    }

    [Fact]
    public async Task GetStatsByFilterAsync_WithCaseTypeFilter_ReturnsFilteredResults()
    {
        ICrimeRepository repository = new InMemoryCrimeRepository();

        var case1 = TheftCase.Create("T003", CaseType.Residential, District.ParseFrom("臺北市大安區"),
            TaiwanDate.Parse("1150329"), TimeSlot.Parse("06~08"), "臺北市大安區");
        var case2 = TheftCase.Create("T004", CaseType.Car, District.ParseFrom("臺北市中山區"),
            TaiwanDate.Parse("1150401"), TimeSlot.Parse("10~12"), "臺北市中山區");

        await repository.AddAsync(case1);
        await repository.AddAsync(case2);

        var filter = new CrimeFilter(CaseType.Residential);
        var (districtCounts, timeSlotCounts) = await repository.GetStatsByFilterAsync(filter);

        districtCounts.Should().HaveCount(1);
        districtCounts[0].District.Should().Be("大安區");
        timeSlotCounts.Should().HaveCount(1);
        timeSlotCounts[0].TimeSlot.Should().Be("06~08");
    }
}
