using FluentAssertions;
using NSubstitute;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Tests.Handlers;

public class GetCrimeByIdQueryHandlerTests
{
    private readonly ICrimeRepository _repository;
    private readonly GetCrimeByIdQueryHandler _handler;

    public GetCrimeByIdQueryHandlerTests()
    {
        _repository = Substitute.For<ICrimeRepository>();
        _handler = new GetCrimeByIdQueryHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_WhenCaseExists_ShouldReturnDetailDto()
    {
        // Arrange
        var theftCase = TheftCase.Create(
            caseNumber: "001",
            caseType: CaseType.Residential,
            district: District.ParseFrom("內湖區"),
            occurredDate: TaiwanDate.Parse("1130101"),
            timeSlot: TimeSlot.Parse("18-20"),
            rawLocation: "臺北市內湖區成功路五段31號");

        _repository
            .GetByIdAsync(theftCase.Id, Arg.Any<CancellationToken>())
            .Returns(theftCase);

        // Act
        var result = await _handler.HandleAsync(new GetCrimeByIdQuery(theftCase.Id));

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(theftCase.Id);
        result.CaseType.Should().Be("住宅竊盜");
        result.District.Should().Be("內湖區");
        result.TimeSlot.Should().Be("18~20");
        result.RawLocation.Should().Be("臺北市內湖區成功路五段31號");
        result.OccurredDate.Should().Be("2024-01-01");
    }

    [Fact]
    public async Task HandleAsync_WhenCaseNotFound_ShouldReturnNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((TheftCase?)null);

        // Act
        var result = await _handler.HandleAsync(new GetCrimeByIdQuery(id));

        // Assert
        result.Should().BeNull();
    }
}
