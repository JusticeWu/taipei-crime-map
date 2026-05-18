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

    [Fact]
    public async Task GetByFilterAsync_WithYearRange_ShouldReturnMatchingCases()
    {
        // Arrange
        var case113 = TheftCase.Create(
            caseNumber: Guid.NewGuid().ToString(),
            caseType: CaseType.Residential,
            district: District.ParseFrom("內湖區"),
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: null,
            rawLocation: "臺北市內湖區成功路五段31號");

        var case112 = TheftCase.Create(
            caseNumber: Guid.NewGuid().ToString(),
            caseType: CaseType.Residential,
            district: District.ParseFrom("內湖區"),
            occurredDate: TaiwanDate.Parse("1120101"),
            timeSlot: null,
            rawLocation: "臺北市內湖區成功路五段31號");

        await _repository.AddRangeAsync([case113, case112]);

        // Act
        var filter = new CrimeFilter(yearFrom: 113, yearTo: 113);
        var results = await _repository.GetByFilterAsync(filter);

        // Assert
        results.Should().Contain(c => c.Id == case113.Id);
        results.Should().NotContain(c => c.Id == case112.Id);
    }

    [Fact]
    public async Task GetByFilterAsync_WithTimeSlot_ShouldReturnMatchingCases()
    {
        // Arrange
        var morning = TheftCase.Create(
            caseNumber: Guid.NewGuid().ToString(),
            caseType: CaseType.Residential,
            district: District.ParseFrom("內湖區"),
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: TimeSlot.Parse("08~10"),
            rawLocation: "臺北市內湖區成功路五段31號");

        var evening = TheftCase.Create(
            caseNumber: Guid.NewGuid().ToString(),
            caseType: CaseType.Residential,
            district: District.ParseFrom("內湖區"),
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: TimeSlot.Parse("18~20"),
            rawLocation: "臺北市內湖區成功路五段31號");

        await _repository.AddRangeAsync([morning, evening]);

        // Act
        var filter = new CrimeFilter(timeSlot: TimeSlot.Parse("08~10"));
        var results = await _repository.GetByFilterAsync(filter);

        // Assert
        results.Should().Contain(c => c.Id == morning.Id);
        results.Should().NotContain(c => c.Id == evening.Id);
    }

    [Fact]
    public async Task GetByFilterAsync_WithNoFilter_ShouldReturnAllCases()
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

        await _repository.AddRangeAsync(cases);

        // Act
        var filter = new CrimeFilter();
        var results = await _repository.GetByFilterAsync(filter);

        // Assert
        foreach (var i in cases)
        {
            results.Should().Contain(c => c.Id == i.Id);
        }
    }

    [Fact]
    public async Task GetByRadiusAsync_ShouldReturnCasesWithinRadius()
    {
        // Arrange
        var nearbyCase = TheftCase.Create(
            caseNumber: Guid.NewGuid().ToString(),
            caseType: CaseType.Residential,
            district: District.ParseFrom("內湖區"),
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: null,
            rawLocation: "臺北市內湖區成功路五段31號",
            coordinate: GeoCoordinate.Create(25.0815, 121.6056));

        var farCase = TheftCase.Create(
            caseNumber: Guid.NewGuid().ToString(),
            caseType: CaseType.Residential,
            district: District.ParseFrom("大安區"),
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: null,
            rawLocation: "臺北市大安區新生南路二段1號",
            coordinate: GeoCoordinate.Create(25.0302, 121.5354));

        await _repository.AddRangeAsync([nearbyCase, farCase]);

        // 搜尋中心: 捷運葫洲站（25.0728, 121.6074）
        var center = GeoCoordinate.Create(25.0728, 121.6074);

        // Act
        // 半徑 2 公里
        var results = await _repository.GetByRadiusAsync(center, radiusKm: 2.0);

        // Assert
        results.Should().Contain(c => c.Id == nearbyCase.Id);
        results.Should().NotContain(c => c.Id == farCase.Id);
    }
}