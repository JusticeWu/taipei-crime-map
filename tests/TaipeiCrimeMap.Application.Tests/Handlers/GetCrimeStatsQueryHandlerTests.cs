using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
    private readonly ICrimeRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly IMemoryCache _memoryCache;
    private readonly GetCrimeStatsQueryHandler _handler;

    public GetCrimeStatsQueryHandlerTests()
    {
        _repository = Substitute.For<ICrimeRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _cache
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);
        _cache
            .SetAsync(
                Arg.Any<string>(), Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _repository.GetCombinedTrendAsync(Arg.Any<string>(), Arg.Any<CrimeFilter>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<(string, int, int)>() as IReadOnlyList<(string, int, int)>);

        _handler = new GetCrimeStatsQueryHandler(
            _repository,
            _cache,
            _memoryCache,
            NullLogger<GetCrimeStatsQueryHandler>.Instance);
    }

    private static readonly IReadOnlyList<TrendSeriesDto> EmptyTrend = Array.Empty<TrendSeriesDto>();

    [Fact]
    public async Task HandleAsync_ShouldReturnDistrictAndTimeSlotDistribution_SortedCorrectly()
    {
        _repository
            .GetStatsByFilterAsync(Arg.Any<CrimeFilter>(), Arg.Any<CancellationToken>())
            .Returns((
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

        var result = await _handler.HandleAsync(new GetCrimeStatsQuery());

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
        _repository
            .GetStatsByFilterAsync(Arg.Any<CrimeFilter>(), Arg.Any<CancellationToken>())
            .Returns((
                (IReadOnlyList<(string District, int Count)>)new List<(string, int)> { ("大安區", 1) },
                (IReadOnlyList<(string TimeSlot, int Count)>)new List<(string, int)>()));

        var query = new GetCrimeStatsQuery(CaseType: CaseType.Residential);

        var first = await _handler.HandleAsync(query);
        var second = await _handler.HandleAsync(query);

        second.Should().BeEquivalentTo(first);
        await _repository.Received(1).GetStatsByFilterAsync(Arg.Any<CrimeFilter>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenL2CacheHit_ShouldReturnDeserializedDataWithoutCallingRepository()
    {
        var query = new GetCrimeStatsQuery(CaseType: CaseType.Car);
        var cacheKey = $"crimes:stats:{query.CaseType}:{query.DistrictName}:{query.YearFrom}:{query.YearTo}";

        var preloaded = new CrimeStatsDto(
            new List<DistrictDistributionDto> { new("信義區", 99) },
            new List<TimeSlotDistributionDto>(),
            EmptyTrend, EmptyTrend, EmptyTrend);
        var preloadedBytes = JsonSerializer.SerializeToUtf8Bytes(preloaded);

        _cache
            .GetAsync(cacheKey, Arg.Any<CancellationToken>())
            .Returns(preloadedBytes);

        var result = await _handler.HandleAsync(query);

        result.DistrictDistribution.Should().ContainSingle(d => d.District == "信義區" && d.Count == 99);
        await _repository.DidNotReceive().GetStatsByFilterAsync(Arg.Any<CrimeFilter>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenCacheThrows_ShouldFallbackToRepository()
    {
        _cache
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Garnet 連線失敗"));

        _repository
            .GetStatsByFilterAsync(Arg.Any<CrimeFilter>(), Arg.Any<CancellationToken>())
            .Returns((
                (IReadOnlyList<(string District, int Count)>)new List<(string, int)>(),
                (IReadOnlyList<(string TimeSlot, int Count)>)new List<(string, int)>()));

        var act = async () => await _handler.HandleAsync(new GetCrimeStatsQuery());

        await act.Should().NotThrowAsync();
        await _repository.Received(1).GetStatsByFilterAsync(Arg.Any<CrimeFilter>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnTrendSeries_GroupedAndSortedByTotal()
    {
        _repository
            .GetStatsByFilterAsync(Arg.Any<CrimeFilter>(), Arg.Any<CancellationToken>())
            .Returns((
                (IReadOnlyList<(string, int)>)new List<(string, int)>(),
                (IReadOnlyList<(string, int)>)new List<(string, int)>()));

        _repository.GetCombinedTrendAsync("TimeSlotCaseType", Arg.Any<CrimeFilter>(), 5, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<(string, int, int)>)new List<(string, int, int)>
            {
                ("18~22 住宅竊盜", 2020, 10),
                ("18~22 住宅竊盜", 2021, 15),
                ("06~08 機車竊盜", 2020, 5),
                ("06~08 機車竊盜", 2021, 3),
            });

        var result = await _handler.HandleAsync(new GetCrimeStatsQuery());

        result.TimeSlotCaseTypeTrend.Should().HaveCount(2);
        result.TimeSlotCaseTypeTrend[0].Label.Should().Be("18~22 住宅竊盜");
        result.TimeSlotCaseTypeTrend[0].Points.Should().HaveCount(2);
        result.TimeSlotCaseTypeTrend[0].Points[0].Year.Should().Be(2020);
        result.TimeSlotCaseTypeTrend[0].Points[0].Count.Should().Be(10);
        result.TimeSlotCaseTypeTrend[1].Label.Should().Be("06~08 機車竊盜");
    }

    [Fact]
    public async Task HandleAsync_TrendSeries_ShouldBeSortedDescendingByTotalCount()
    {
        _repository
            .GetStatsByFilterAsync(Arg.Any<CrimeFilter>(), Arg.Any<CancellationToken>())
            .Returns((
                (IReadOnlyList<(string, int)>)new List<(string, int)>(),
                (IReadOnlyList<(string, int)>)new List<(string, int)>()));

        _repository.GetCombinedTrendAsync("DistrictCaseType", Arg.Any<CrimeFilter>(), 5, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<(string, int, int)>)new List<(string, int, int)>
            {
                ("松山區 住宅竊盜", 2020, 2),
                ("大安區 機車竊盜", 2020, 50),
            });

        var result = await _handler.HandleAsync(new GetCrimeStatsQuery());

        result.DistrictCaseTypeTrend[0].Label.Should().Be("大安區 機車竊盜");
        result.DistrictCaseTypeTrend[1].Label.Should().Be("松山區 住宅竊盜");
    }
}
