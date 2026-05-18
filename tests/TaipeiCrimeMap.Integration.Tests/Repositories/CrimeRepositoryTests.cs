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
}