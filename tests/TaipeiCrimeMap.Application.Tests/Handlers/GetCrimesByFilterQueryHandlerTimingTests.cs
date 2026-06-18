using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Application.Interfaces;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Tests.Handlers;

public class GetCrimesByFilterQueryHandlerTimingTests
{
    private readonly ICrimeRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly IMemoryCache _memoryCache;

    public GetCrimesByFilterQueryHandlerTimingTests()
    {
        _repository = Substitute.For<ICrimeRepository>();
        _repository
            .GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        _cache = Substitute.For<IDistributedCache>();
        _cache
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);
        _cache
            .SetAsync(
                Arg.Any<string>(), Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    private GetCrimesByFilterQueryHandler CreateHandler(ITimingTracker timing) =>
        new(_repository,
            _cache,
            _memoryCache,
            timing,
            NullLogger<GetCrimesByFilterQueryHandler>.Instance);

    [Fact]
    public async Task HandleAsync_WhenTimingEnabled_ShouldCallLogSummary()
    {
        // Arrange
        var mockTiming = Substitute.For<ITimingTracker>();
        mockTiming.Track(Arg.Any<string>())
                  .Returns(Substitute.For<IDisposable>());
        var handler = CreateHandler(mockTiming);

        // Act
        await handler.HandleAsync(new GetCrimesByFilterQuery());

        // Assert
        mockTiming.Received(1).LogSummary();
    }

    [Fact]
    public async Task HandleAsync_WhenTimingEnabled_ShouldTrackL1CacheStage()
    {
        // Arrange
        var mockTiming = Substitute.For<ITimingTracker>();
        mockTiming.Track(Arg.Any<string>())
                  .Returns(Substitute.For<IDisposable>());
        var handler = CreateHandler(mockTiming);

        // Act
        await handler.HandleAsync(new GetCrimesByFilterQuery());

        // Assert
        mockTiming.Received(1).Track("L1-Cache");
    }

    [Fact]
    public async Task HandleAsync_WhenL1CacheHits_ShouldDisposeL1CacheStageBeforeCallingLogSummary()
    {
        // Arrange — 回歸測試：LogSummary() 必須在 using(_timing.Track("L1-Cache")) 結束、
        // StageTimer.Dispose 把耗時寫入 _records 之後才被呼叫；
        // 否則命中 L1 時 _records 是空的，LogSummary 會直接略過、不輸出任何內容（L011 後修正的 bug）
        var tracker = new OrderTrackingTimingTracker();
        var handler = CreateHandler(tracker);
        var query = new GetCrimesByFilterQuery();
        var cacheKey = $"crimes:filter:{query.CaseType}:{query.DistrictName}:{query.YearFrom}:{query.YearTo}:{query.RawTimeSlot}:{query.Page}:{query.PageSize}:{query.SortBy}:{query.SortOrder}";
        _memoryCache.Set(cacheKey, new PagedResult<TheftCaseDto>(new List<TheftCaseDto>(), 0, query.Page, query.PageSize, 0));

        // Act
        await handler.HandleAsync(query);

        // Assert — LogSummary 被呼叫時，"L1-Cache" 階段必須已經被 Dispose（已記錄耗時）
        tracker.StagesDisposedBeforeFirstLogSummary.Should().Contain("L1-Cache");
    }

    /// <summary>
    /// 模擬真實 TimingTracker 的關鍵行為：用來驗證 Track() 回傳的 IDisposable
    /// 是否在 LogSummary() 被呼叫「之前」就已經 Dispose（即階段已記錄）。
    /// </summary>
    private sealed class OrderTrackingTimingTracker : ITimingTracker
    {
        private readonly List<string> _disposedStages = new();
        private bool _summaryLogged;

        public List<string> StagesDisposedBeforeFirstLogSummary { get; private set; } = new();

        public IDisposable Track(string stageName) => new StageScope(() => _disposedStages.Add(stageName));

        public void LogSummary()
        {
            if (_summaryLogged) return;
            _summaryLogged = true;
            StagesDisposedBeforeFirstLogSummary = new List<string>(_disposedStages);
        }

        private sealed class StageScope : IDisposable
        {
            private readonly Action _onDispose;
            private bool _disposed;
            public StageScope(Action onDispose) => _onDispose = onDispose;
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _onDispose();
            }
        }
    }

    [Fact]
    public async Task HandleAsync_WhenTimingDisabled_NullTracker_ShouldNotThrow()
    {
        // Arrange — 模擬 Timing:Enabled = false 時注入的空追蹤器行為
        var nullTracker = Substitute.For<ITimingTracker>();
        nullTracker.Track(Arg.Any<string>())
                   .Returns(Substitute.For<IDisposable>());
        var handler = CreateHandler(nullTracker);

        // Act
        var act = async () => await handler.HandleAsync(new GetCrimesByFilterQuery());

        // Assert
        await act.Should().NotThrowAsync();
    }
}
