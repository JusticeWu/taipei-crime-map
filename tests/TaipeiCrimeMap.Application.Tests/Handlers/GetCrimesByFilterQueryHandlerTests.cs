using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Exceptions;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Tests.Handlers;

public class GetCrimesByFilterQueryHandlerTests
{
    private readonly Mock<ICrimeRepository> _repositoryMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly GetCrimesByFilterQueryHandler _handler;

    public GetCrimesByFilterQueryHandlerTests()
    {
        _repositoryMock = new Mock<ICrimeRepository>();
        _cacheMock = new Mock<IDistributedCache>();

        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new GetCrimesByFilterQueryHandler(
            _repositoryMock.Object,
            _cacheMock.Object,
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

        _repositoryMock.Setup(r => r.GetPagedByFilterAsync(
                It.IsAny<CrimeFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<TheftCase>)cases, cases.Count));

        var query = new GetCrimesByFilterQuery(CaseType: CaseType.Residential);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Data.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Data[0].CaseNumber.Should().Be("001");
        result.Data[0].CaseType.Should().Be("住宅竊盜");
        result.Data[0].District.Should().Be("內湖區");
        result.Data[0].OccurredDate.Should().Be("2024-01-01");
        result.Data[0].TimeSlot.Should().Be("18~20");
        result.Data[0].RawLocation.Should().Be("臺北市內湖區成功路五段31號");
    }

    [Fact]
    public async Task HandleAsync_WithInvalidTimeSlot_ShouldThrowDomainException()
    {
        // Arrange
        var query = new GetCrimesByFilterQuery(RawTimeSlot: "test");

        // Act
        var act = async () => await _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*test*");
    }

    [Fact]
    public async Task HandleAsync_WithNoFilter_ShouldCallGetPagedByFilterAsync()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetPagedByFilterAsync(
                It.IsAny<CrimeFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var query = new GetCrimesByFilterQuery();

        // Act
        await _handler.HandleAsync(query);

        // Assert
        _repositoryMock.Verify(r => r.GetPagedByFilterAsync(
                It.IsAny<CrimeFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithDistrictFilter_ShouldPassCorrectFilterToRepository()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetPagedByFilterAsync(
                It.IsAny<CrimeFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var query = new GetCrimesByFilterQuery(DistrictName: "大安區");

        // Act
        await _handler.HandleAsync(query);

        // Assert
        _repositoryMock.Verify(r => r.GetPagedByFilterAsync(
                It.Is<CrimeFilter>(f => f.District != null && f.District.Name == "大安區"),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SecondCallWithSameQuery_ShouldReturnCachedResult()
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

        _repositoryMock.Setup(r => r.GetPagedByFilterAsync(
                It.IsAny<CrimeFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<TheftCase>)cases, cases.Count));

        var stored = new Dictionary<string, byte[]>();
        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) =>
                Task.FromResult<byte[]?>(stored.TryGetValue(key, out var v) ? v : null));
        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (key, value, _, _) => stored[key] = value)
            .Returns(Task.CompletedTask);

        var query = new GetCrimesByFilterQuery(CaseType: CaseType.Residential);

        // Act
        var firstResult = await _handler.HandleAsync(query);
        var secondResult = await _handler.HandleAsync(query);

        // Assert
        secondResult.Should().BeEquivalentTo(firstResult);
        _repositoryMock.Verify(r => r.GetPagedByFilterAsync(
                It.IsAny<CrimeFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DifferentQueries_ShouldCallRepositoryTwice()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetPagedByFilterAsync(
                It.IsAny<CrimeFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var query1 = new GetCrimesByFilterQuery(DistrictName: "大安區");
        var query2 = new GetCrimesByFilterQuery(DistrictName: "內湖區");

        // Act
        await _handler.HandleAsync(query1);
        await _handler.HandleAsync(query2);

        // Assert
        _repositoryMock.Verify(r => r.GetPagedByFilterAsync(
                It.IsAny<CrimeFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WhenCacheHit_ShouldReturnDeserializedData()
    {
        // Arrange
        var query = new GetCrimesByFilterQuery(CaseType: CaseType.Car);
        var cacheKey = $"crimes:filter:{query.CaseType}:{query.DistrictName}:{query.YearFrom}:{query.YearTo}:{query.RawTimeSlot}:{query.Page}:{query.PageSize}";

        var preloaded = new PagedResult<TheftCaseDto>(
            new List<TheftCaseDto> { new() { CaseNumber = "cached-001", CaseType = "汽車竊盜", District = "信義區" } },
            Total: 1, Page: 1, PageSize: 200, TotalPages: 1);
        var preloadedBytes = JsonSerializer.SerializeToUtf8Bytes(preloaded);

        _cacheMock
            .Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preloadedBytes);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Data.Should().HaveCount(1);
        result.Data[0].CaseNumber.Should().Be("cached-001");
        _repositoryMock.Verify(r => r.GetPagedByFilterAsync(
                It.IsAny<CrimeFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
