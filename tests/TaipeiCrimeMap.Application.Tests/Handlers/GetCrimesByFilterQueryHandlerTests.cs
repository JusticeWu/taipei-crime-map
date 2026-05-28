using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Tests.Handlers;

public class GetCrimesByFilterQueryHandlerTests
{
    private readonly Mock<ICrimeRepository> _repositoryMock;
    private readonly GetCrimesByFilterQueryHandler _handler;

    public GetCrimesByFilterQueryHandlerTests()
    {
        _repositoryMock = new Mock<ICrimeRepository>();

        _handler = new GetCrimesByFilterQueryHandler(
            _repositoryMock.Object, 
            NullLogger<GetCrimesByFilterQueryHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnMappingDtos()
    {
        // Arrange
        var cases = new List<TheftCase>
        {
            TheftCase.Create(
                caseNumber: "001",
                caseType: CaseType.Residential,
                district: District.ParseFrom("內湖區"),
                occurredDate: TaiwanDate.Parse("1130101"),
                timeSlot: TimeSlot.Parse("18-20"),
                rawLocation: "臺北市內湖區成功路五段31號")
        };

        _repositoryMock.Setup(
            r => r.GetByFilterAsync(
                It.IsAny<CrimeFilter>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cases);

        var query = new GetCrimesByFilterQuery(CaseType: CaseType.Residential);

        // Act
        var results = await _handler.HandleAsync(query);

        // Assert
        results.Should().HaveCount(1);
        results[0].CaseNumber.Should().Be("001");
        results[0].CaseType.Should().Be("住宅竊盜");
        results[0].District.Should().Be("內湖區");
        results[0].OccurredDate.Should().Be("2024-01-01");
        results[0].TimeSlot.Should().Be("18~20");
        results[0].RawLocation.Should().Be("臺北市內湖區成功路五段31號");
    }
}