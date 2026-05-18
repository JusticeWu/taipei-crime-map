using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Integration.Tests.Repositories;

public class CrimeRepositoryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly ICrimeRepository _repository;

    public CrimeRepositoryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _repository = _factory.Services.GetRequiredService<ICrimeRepository>();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistData()
    {
        // Arrange
        var theftCase = TheftCase.Create(
            caseNumber: Guid.NewGuid().ToString(),
            caseType: CaseType.Residential,
            district: District.ParseFrom("內湖區"),
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: null,
            rawLocation: "臺北市內湖區成功路五段31號");

        // Act
        await _repository.AddAsync(theftCase);

        // Assert
        var result = await _repository.GetByIdAsync(theftCase.Id);
        result.Should().NotBeNull();
        result!.CaseNumber.Should().Be(theftCase.CaseNumber);
    }

    [Fact]
    public async Task AddRangeAsync_ShouldPersistAllData()
    {
        // Arrange
        var cases = new List<TheftCase>();
        for (int i = 0; i < 3; i++)
        {
            cases.Add(TheftCase.Create(
                caseNumber: Guid.NewGuid().ToString(),
                caseType: CaseType.Residential,
                district: District.ParseFrom("內湖區"),
                occurredDate: TaiwanDate.Parse("1130101"),
                timeSlot: null,
                rawLocation: "臺北市內湖區成功路五段31號"));
        }

        // Act
        await _repository.AddRangeAsync(cases);

        // Assert
        foreach (var c in cases)
        {
            var result = await _repository.GetByIdAsync(c.Id);
            result.Should().NotBeNull();
            result!.CaseNumber.Should().Be(c.CaseNumber);
        }
    }

    [Fact]
    public async Task GetByFilterAsync_WithCaseType_ShouldReturnMatchingCases()
    {
        // Arrange
        var residential = TheftCase.Create(
            caseNumber: Guid.NewGuid().ToString(),
            caseType: CaseType.Residential,
            district: District.ParseFrom("內湖區"),
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: null,
            rawLocation: "臺北市內湖區成功路五段31號");

        var car = TheftCase.Create(
            caseNumber: Guid.NewGuid().ToString(),
            caseType: CaseType.Car,
            district: District.ParseFrom("內湖區"),
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: null,
            rawLocation: "臺北市內湖區成功路五段31號");

        await _repository.AddRangeAsync([residential, car]);

        // Act
        var filter = new CrimeFilter(caseType: CaseType.Residential);
        var results = await _repository.GetByFilterAsync(filter);

        // Assert
        results.Should().Contain(c => c.Id == residential.Id);
        results.Should().NotContain(c => c.Id == car.Id);
    }

    [Fact]
    public async Task GetByFilterAsync_WithDistrict_ShouldReturnMatchingCases()
    {
        // Arrange
        var neihu = TheftCase.Create(
            caseNumber: Guid.NewGuid().ToString(),
            caseType: CaseType.Residential,
            district: District.ParseFrom("內湖區"),
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: null,
            rawLocation: "臺北市內湖區成功路五段31號");

        var daan = TheftCase.Create(
            caseNumber: Guid.NewGuid().ToString(),
            caseType: CaseType.Residential,
            district: District.ParseFrom("大安區"),
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: null,
            rawLocation: "臺北市大安區新生南路二段1號");

        await _repository.AddRangeAsync([neihu, daan]);

        // Act
        var filter = new CrimeFilter(district: District.ParseFrom("內湖區"));
        var results = await _repository.GetByFilterAsync(filter);

        // Assert
        results.Should().Contain(c => c.Id == neihu.Id);
        results.Should().NotContain(c => c.Id == daan.Id);
    }
}