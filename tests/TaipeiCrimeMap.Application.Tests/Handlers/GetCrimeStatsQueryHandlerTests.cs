using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Tests.Handlers;

public class GetCrimeStatsQueryHandlerTests
{
    private readonly Mock<ICrimeRepository> _repositoryMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly IMemoryCache _memoryCache;
    private readonly GetCrimeStatsQueryHandler _handler;

    public GetCrimeStatsQueryHandlerTests()
    {
        _repositoryMock = new Mock<ICrimeRepository>();
        _cacheMock = new Mock<IDistributedCache>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new GetCrimeStatsQueryHandler(
            _repositoryMock.Object,
            _cacheMock.Object,
            _memoryCache,
            NullLogger<GetCrimeStatsQueryHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnDistrictAndTimeSlotDistribution_SortedCorrectly()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetStatsByFilterAsync(It.IsAny<CrimeFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                (IReadOnlyList<(string District, int Count)>)new List<(string, int)>
                {
                    ("大安區", 5),
                    ("信義區", 10),
                },
                (IReadOnlyList<(string TimeSlot, int Count)>)new List<(string, int)>
                {
                    ("14~16", 3),
                    ("00~02", 7),
                }));

        // Act
        var result = await _handler.HandleAsync(new GetCrimeStatsQuery());

        // Assert - 行政區依案件數由多到少；時段依字串排序由小到大
        result.DistrictDistribution.Should().Equal(
            new DistrictDistributionDto("信義區", 10),
            new DistrictDistributionDto("大安區", 5));
        result.TimeSlotDistribution.Should().Equal(
            new TimeSlotDistributionDto("00~02", 7),
            new TimeSlotDistributionDto("14~16", 3));
    }

    [Fact]
    public async Task HandleAsync_SecondCallWithSameQuery_ShouldReturnCachedResultWithoutCallingRepository()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetStatsByFilterAsync(It.IsAny<CrimeFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                (IReadOnlyList<(string District, int Count)>)new List<(string, int)> { ("大安區", 1) },
                (IReadOnlyList<(string TimeSlot, int Count)>)new List<(string, int)>()));

        var query = new GetCrimeStatsQuery(CaseType: CaseType.Residential);

        // Act
        var first = await _handler.HandleAsync(query);
        var second = await _handler.HandleAsync(query);

        // Assert
        second.Should().BeEquivalentTo(first);
        _repositoryMock.Verify(r => r.GetStatsByFilterAsync(It.IsAny<CrimeFilter>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenL2CacheHit_ShouldReturnDeserializedDataWithoutCallingRepository()
    {
        // Arrange
        var query = new GetCrimeStatsQuery(CaseType: CaseType.Car);
        var cacheKey = $"crimes:stats:{query.CaseType}:{query.DistrictName}:{query.YearFrom}:{query.YearTo}";

        var preloaded = new CrimeStatsDto(
            new List<DistrictDistributionDto> { new("信義區", 99) },
            new List<TimeSlotDistributionDto>());
        var preloadedBytes = JsonSerializer.SerializeToUtf8Bytes(preloaded);

        _cacheMock
            .Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preloadedBytes);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.DistrictDistribution.Should().ContainSingle(d => d.District == "信義區" && d.Count == 99);
        _repositoryMock.Verify(r => r.GetStatsByFilterAsync(It.IsAny<CrimeFilter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCacheThrows_ShouldFallbackToRepository()
    {
        // Arrange
        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Garnet 連線失敗"));

        _repositoryMock
            .Setup(r => r.GetStatsByFilterAsync(It.IsAny<CrimeFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                (IReadOnlyList<(string District, int Count)>)new List<(string, int)>(),
                (IReadOnlyList<(string TimeSlot, int Count)>)new List<(string, int)>()));

        // Act
        var act = async () => await _handler.HandleAsync(new GetCrimeStatsQuery());

        // Assert
        await act.Should().NotThrowAsync();
        _repositoryMock.Verify(r => r.GetStatsByFilterAsync(It.IsAny<CrimeFilter>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
