using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Application.Interfaces;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Exceptions;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Tests.Handlers;

public class GetCrimesByFilterQueryHandlerTests
{
    private readonly ICrimeRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly IMemoryCache _memoryCache;
    private readonly ITimingTracker _timing;
    private readonly GetCrimesByFilterQueryHandler _handler;

    public GetCrimesByFilterQueryHandlerTests()
    {
        _repository = Substitute.For<ICrimeRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _timing = Substitute.For<ITimingTracker>();
        _timing.Track(Arg.Any<string>()).Returns(Substitute.For<IDisposable>());

        _cache
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);
        _cache
            .SetAsync(
                Arg.Any<string>(), Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _handler = new GetCrimesByFilterQueryHandler(
            _repository,
            _cache,
            _memoryCache,
            _timing,
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

        _repository.GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)cases, cases.Count));

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
        _repository.GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var query = new GetCrimesByFilterQuery();

        // Act
        await _handler.HandleAsync(query);

        // Assert
        await _repository.Received(1).GetPagedByFilterAsync(
            Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithDistrictFilter_ShouldPassCorrectFilterToRepository()
    {
        // Arrange
        _repository.GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var query = new GetCrimesByFilterQuery(DistrictName: "大安區");

        // Act
        await _handler.HandleAsync(query);

        // Assert
        await _repository.Received(1).GetPagedByFilterAsync(
            Arg.Is<CrimeFilter>(f => f.District != null && f.District.Name == "大安區"),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
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

        _repository.GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)cases, cases.Count));

        var query = new GetCrimesByFilterQuery(CaseType: CaseType.Residential);

        // Act - 第一次呼叫走 DB，結果寫入 L1；第二次命中 L1
        var firstResult = await _handler.HandleAsync(query);
        var secondResult = await _handler.HandleAsync(query);

        // Assert
        secondResult.Should().BeEquivalentTo(firstResult);
        await _repository.Received(1).GetPagedByFilterAsync(
            Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DifferentQueries_ShouldCallRepositoryTwice()
    {
        // Arrange
        _repository.GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var query1 = new GetCrimesByFilterQuery(DistrictName: "大安區");
        var query2 = new GetCrimesByFilterQuery(DistrictName: "內湖區");

        // Act
        await _handler.HandleAsync(query1);
        await _handler.HandleAsync(query2);

        // Assert
        await _repository.Received(2).GetPagedByFilterAsync(
            Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 快取拋出例外時，應該 fallthrough 到 Repository，不拋出例外
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCacheThrows_ShouldFallbackToRepository()
    {
        // Arrange
        _cache
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Redis 連線失敗"));

        _repository
            .GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var query = new GetCrimesByFilterQuery();

        // Act
        var act = async () => await _handler.HandleAsync(query);
        var result = await act.Should().NotThrowAsync();

        // Assert
        result.Subject.Should().NotBeNull();
        await _repository.Received(1).GetPagedByFilterAsync(
            Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
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

        _cache
            .GetAsync(cacheKey, Arg.Any<CancellationToken>())
            .Returns(preloadedBytes);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Data.Should().HaveCount(1);
        result.Data[0].CaseNumber.Should().Be("cached-001");
        await _repository.DidNotReceive().GetPagedByFilterAsync(
            Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────
    // L1 MemoryCache 測試
    // ──────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WhenL1Hit_ShouldNotCallRepository()
    {
        // Arrange - 預先填入 L1
        var query = new GetCrimesByFilterQuery(CaseType: CaseType.Car);
        var cacheKey = $"crimes:filter:{query.CaseType}:{query.DistrictName}:{query.YearFrom}:{query.YearTo}:{query.RawTimeSlot}:{query.Page}:{query.PageSize}";

        var preloaded = new PagedResult<TheftCaseDto>(
            new List<TheftCaseDto> { new() { CaseNumber = "l1-001", CaseType = "汽車竊盜" } },
            Total: 1, Page: 1, PageSize: 200, TotalPages: 1);
        _memoryCache.Set(cacheKey, preloaded, TimeSpan.FromMinutes(1));

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().BeEquivalentTo(preloaded);
        await _repository.DidNotReceive().GetPagedByFilterAsync(
            Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenL1MissAndGarnetFails_SecondCallShouldHitL1AndCallRepositoryOnce()
    {
        // Arrange - L1 空、Garnet 拋出例外
        _cache
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Garnet 連線失敗"));

        _repository
            .GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var query = new GetCrimesByFilterQuery();

        // Act - 第一次：L1 miss + L2 fail → DB 呼叫，結果寫入 L1
        await _handler.HandleAsync(query);
        // Act - 第二次：L1 命中
        await _handler.HandleAsync(query);

        // Assert - Repository 只被呼叫一次
        await _repository.Received(1).GetPagedByFilterAsync(
            Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenBothL1AndGarnetFail_ShouldFallbackToRepository()
    {
        // Arrange - L1 substitute 拋出例外、Garnet substitute 拋出例外
        var memoryCache = Substitute.For<IMemoryCache>();
        memoryCache
            .TryGetValue(Arg.Any<object>(), out Arg.Any<object?>())
            .Returns(x => throw new Exception("MemoryCache 故障"));
        var cacheEntry = Substitute.For<ICacheEntry>();
        memoryCache.CreateEntry(Arg.Any<object>()).Returns(cacheEntry);

        _cache
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Garnet 連線失敗"));

        _repository
            .GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var handler = new GetCrimesByFilterQueryHandler(
            _repository,
            _cache,
            memoryCache,
            _timing,
            NullLogger<GetCrimesByFilterQueryHandler>.Instance);

        // Act
        var result = await handler.HandleAsync(new GetCrimesByFilterQuery());

        // Assert
        result.Should().NotBeNull();
        await _repository.Received(1).GetPagedByFilterAsync(
            Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────
    // L1/L2 故障情境組合測試
    // ──────────────────────────────────────────────

    /// <summary>
    /// 情境一：L1未命中、L2未命中 → 第一次打DB，結果存入L1和L2；第二次L1命中
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenL1MissL2Miss_SecondCallShouldHitL1AndCallDbOnce()
    {
        // Arrange
        var memoryCache = Substitute.For<IMemoryCache>();
        var cacheEntry = Substitute.For<ICacheEntry>();

        // L1: 第一次 miss；Set 後變為 hit
        bool l1Hit = false;
        object? l1Value = null;
        memoryCache
            .TryGetValue(Arg.Any<object>(), out Arg.Any<object?>())
            .Returns(x =>
            {
                x[1] = l1Value;
                return l1Hit;
            });
        cacheEntry
            .When(e => e.Value = Arg.Any<object?>())
            .Do(callInfo =>
            {
                l1Value = callInfo.Arg<object?>();
                l1Hit = true;
            });
        memoryCache.CreateEntry(Arg.Any<object>()).Returns(cacheEntry);

        // L2: 永遠 miss
        var l2 = Substitute.For<IDistributedCache>();
        l2.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
          .Returns((byte[]?)null);
        l2.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
          .Returns(Task.CompletedTask);

        _repository
            .GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var handler = new GetCrimesByFilterQueryHandler(
            _repository, l2, memoryCache,
            _timing,
            NullLogger<GetCrimesByFilterQueryHandler>.Instance);

        var query = new GetCrimesByFilterQuery();

        // Act
        await handler.HandleAsync(query); // L1 miss + L2 miss → DB(1)，結果存入 L1 和 L2
        await handler.HandleAsync(query); // L1 hit

        // Assert
        await _repository.Received(1).GetPagedByFilterAsync(
            Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 情境二：L1未命中、L2故障 → 第一次打DB，結果存入L1；第二次L1命中
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenL1MissL2Fault_SecondCallShouldHitL1AndCallDbOnce()
    {
        // Arrange
        var memoryCache = Substitute.For<IMemoryCache>();
        var cacheEntry = Substitute.For<ICacheEntry>();

        // L1: 第一次 miss；Set 後變為 hit
        bool l1Hit = false;
        object? l1Value = null;
        memoryCache
            .TryGetValue(Arg.Any<object>(), out Arg.Any<object?>())
            .Returns(x =>
            {
                x[1] = l1Value;
                return l1Hit;
            });
        cacheEntry
            .When(e => e.Value = Arg.Any<object?>())
            .Do(callInfo =>
            {
                l1Value = callInfo.Arg<object?>();
                l1Hit = true;
            });
        memoryCache.CreateEntry(Arg.Any<object>()).Returns(cacheEntry);

        // L2: 永遠故障（GetAsync 和 SetAsync 都拋出例外）
        var l2 = Substitute.For<IDistributedCache>();
        l2.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new Exception("Garnet 故障"));
        l2.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new Exception("Garnet 故障"));

        _repository
            .GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var handler = new GetCrimesByFilterQueryHandler(
            _repository, l2, memoryCache,
            _timing,
            NullLogger<GetCrimesByFilterQueryHandler>.Instance);

        var query = new GetCrimesByFilterQuery();

        // Act
        await handler.HandleAsync(query); // L1 miss + L2 故障 → DB(1)，結果存入 L1
        await handler.HandleAsync(query); // L1 hit（L2 故障不影響）

        // Assert
        await _repository.Received(1).GetPagedByFilterAsync(
            Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 情境三：L1故障、L2未命中 → 第一次打DB，結果存入L2；第二次L1仍故障、L2命中
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenL1FaultL2Miss_SecondCallL1FaultL2ShouldHit()
    {
        // Arrange - L1 永遠拋出例外
        var memoryCache = Substitute.For<IMemoryCache>();
        memoryCache
            .TryGetValue(Arg.Any<object>(), out Arg.Any<object?>())
            .Returns(x => throw new Exception("L1 故障"));
        var cacheEntry = Substitute.For<ICacheEntry>();
        memoryCache.CreateEntry(Arg.Any<object>()).Returns(cacheEntry);

        // L2: 第一次 miss，SetAsync 擷取 bytes，第二次改為回傳 bytes（hit）
        var l2 = Substitute.For<IDistributedCache>();
        byte[]? capturedBytes = null;
        l2.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
          .Returns((byte[]?)null);
        l2.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
          .Returns(Task.CompletedTask)
          .AndDoes(callInfo => capturedBytes = callInfo.Arg<byte[]>());

        _repository
            .GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var handler = new GetCrimesByFilterQueryHandler(
            _repository, l2, memoryCache,
            _timing,
            NullLogger<GetCrimesByFilterQueryHandler>.Instance);

        var query = new GetCrimesByFilterQuery();

        // Act - 第一次：L1 故障 + L2 miss → DB(1)，結果存入 L2
        await handler.HandleAsync(query);

        // 重新 setup L2 GetAsync 回傳已存的 bytes
        l2.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
          .Returns(_ => capturedBytes);

        // Act - 第二次：L1 仍故障，L2 hit
        await handler.HandleAsync(query);

        // Assert
        await _repository.Received(1).GetPagedByFilterAsync(
            Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 情境四：L1故障、L2故障 → 兩次都打DB
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenBothL1AndL2AlwaysFault_ShouldCallDbTwice()
    {
        // Arrange - L1 永遠拋出例外
        var memoryCache = Substitute.For<IMemoryCache>();
        memoryCache
            .TryGetValue(Arg.Any<object>(), out Arg.Any<object?>())
            .Returns(x => throw new Exception("L1 故障"));
        var cacheEntry = Substitute.For<ICacheEntry>();
        memoryCache.CreateEntry(Arg.Any<object>()).Returns(cacheEntry);

        // L2 永遠拋出例外
        var l2 = Substitute.For<IDistributedCache>();
        l2.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new Exception("Garnet 故障"));
        l2.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new Exception("Garnet 故障"));

        _repository
            .GetPagedByFilterAsync(
                Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<TheftCase>)new List<TheftCase>(), 0));

        var handler = new GetCrimesByFilterQueryHandler(
            _repository, l2, memoryCache,
            _timing,
            NullLogger<GetCrimesByFilterQueryHandler>.Instance);

        var query = new GetCrimesByFilterQuery();

        // Act - 兩次都因快取完全失效而打 DB
        await handler.HandleAsync(query);
        await handler.HandleAsync(query);

        // Assert
        await _repository.Received(2).GetPagedByFilterAsync(
            Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
