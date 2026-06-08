using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Application.Interfaces;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Tests.Handlers;

public class GetCrimesByFilterQueryHandlerTimingTests
{
    private readonly Mock<ICrimeRepository> _repositoryMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly IMemoryCache _memoryCache;

    public GetCrimesByFilterQueryHandlerTimingTests()
    {
        _repositoryMock = new Mock<ICrimeRepository>();
        _repositoryMock
            .Setup(r => r.GetPagedByFilterAsync(
                It.IsAny<CrimeFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        _cacheMock = new Mock<IDistributedCache>();
        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    private GetCrimesByFilterQueryHandler CreateHandler(ITimingTracker timing) =>
        new(_repositoryMock.Object,
            _cacheMock.Object,
            _memoryCache,
            timing,
            NullLogger<GetCrimesByFilterQueryHandler>.Instance);

    [Fact]
    public async Task HandleAsync_WhenTimingEnabled_ShouldCallLogSummary()
    {
        // Arrange
        var mockTiming = new Mock<ITimingTracker>();
        mockTiming.Setup(t => t.Track(It.IsAny<string>()))
                  .Returns(new Mock<IDisposable>().Object);
        var handler = CreateHandler(mockTiming.Object);

        // Act
        await handler.HandleAsync(new GetCrimesByFilterQuery());

        // Assert
        mockTiming.Verify(t => t.LogSummary(), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenTimingEnabled_ShouldTrackL1CacheStage()
    {
        // Arrange
        var mockTiming = new Mock<ITimingTracker>();
        mockTiming.Setup(t => t.Track(It.IsAny<string>()))
                  .Returns(new Mock<IDisposable>().Object);
        var handler = CreateHandler(mockTiming.Object);

        // Act
        await handler.HandleAsync(new GetCrimesByFilterQuery());

        // Assert
        mockTiming.Verify(t => t.Track("L1-Cache"), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenTimingDisabled_NullTracker_ShouldNotThrow()
    {
        // Arrange — 模擬 Timing:Enabled = false 時注入的空追蹤器行為
        var nullTracker = new Mock<ITimingTracker>();
        nullTracker.Setup(t => t.Track(It.IsAny<string>()))
                   .Returns(new Mock<IDisposable>().Object);
        var handler = CreateHandler(nullTracker.Object);

        // Act
        var act = async () => await handler.HandleAsync(new GetCrimesByFilterQuery());

        // Assert
        await act.Should().NotThrowAsync();
    }
}
