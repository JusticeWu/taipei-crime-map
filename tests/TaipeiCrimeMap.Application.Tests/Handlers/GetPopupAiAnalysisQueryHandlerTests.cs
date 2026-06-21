using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.Services;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Tests.Handlers;

public class GetPopupAiAnalysisQueryHandlerTests
{
    private readonly ICrimeRepository _repository;
    private readonly ILlmAnalysisService _llm;
    private readonly IDistributedCache _cache;
    private readonly IMemoryCache _memoryCache;
    private readonly GetPopupAiAnalysisQueryHandler _handler;

    public GetPopupAiAnalysisQueryHandlerTests()
    {
        _repository = Substitute.For<ICrimeRepository>();
        _llm = Substitute.For<ILlmAnalysisService>();
        _cache = Substitute.For<IDistributedCache>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _cache.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _handler = new GetPopupAiAnalysisQueryHandler(
            _repository, _llm, _cache, _memoryCache,
            NullLogger<GetPopupAiAnalysisQueryHandler>.Instance);
    }

    private static TheftCase CreateCase(Guid id, string district, CaseType caseType, int startHour)
    {
        return TheftCase.Reconstitute(
            id, 1001, caseType,
            District.ParseFrom(district),
            TaiwanDate.Parse("1130101"),
            TimeSlot.Parse($"{startHour:D2}~{startHour + 2:D2}"),
            "臺北市" + district + "某路",
            null, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task HandleAsync_CaseNotFound_ReturnsNull()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TheftCase?)null);

        var result = await _handler.HandleAsync(new GetPopupAiAnalysisQuery(Guid.NewGuid()));

        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ValidCase_CallsLlmAndReturnAnalysis()
    {
        var id = Guid.NewGuid();
        var theftCase = CreateCase(id, "內湖區", CaseType.Motorcycle, 18);
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(theftCase);
        _repository.GetGroupedYearlyTrendAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<int>>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int, int)> { (2023, 50), (2024, 60) });
        _llm.GenerateAnalysisAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("此區域案件呈上升趨勢。");

        var result = await _handler.HandleAsync(new GetPopupAiAnalysisQuery(id));

        result.Should().Be("此區域案件呈上升趨勢。");
        await _llm.Received(1).GenerateAnalysisAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SameGroup_SecondCallHitsL1Cache()
    {
        var id = Guid.NewGuid();
        var theftCase = CreateCase(id, "內湖區", CaseType.Motorcycle, 18);
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(theftCase);
        _repository.GetGroupedYearlyTrendAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<int>>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int, int)> { (2023, 50) });
        _llm.GenerateAnalysisAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("分析結果");

        await _handler.HandleAsync(new GetPopupAiAnalysisQuery(id));
        var second = await _handler.HandleAsync(new GetPopupAiAnalysisQuery(id));

        second.Should().Be("分析結果");
        await _llm.Received(1).GenerateAnalysisAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_LlmFails_ReturnsFallbackSummary()
    {
        var id = Guid.NewGuid();
        var theftCase = CreateCase(id, "信義區", CaseType.Residential, 14);
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(theftCase);
        _repository.GetGroupedYearlyTrendAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<int>>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int, int)> { (2023, 30) });
        _llm.GenerateAnalysisAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new HttpRequestException("API Error"));

        var result = await _handler.HandleAsync(new GetPopupAiAnalysisQuery(id));

        result.Should().Contain("AI 分析暫時無法使用");
        result.Should().Contain("2023年30件");
    }

    [Fact]
    public async Task HandleAsync_CacheKey_UsesGroupNotRawValues()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var case1 = CreateCase(id1, "內湖區", CaseType.Car, 16);
        var case2 = CreateCase(id2, "南港區", CaseType.Bicycle, 18);
        _repository.GetByIdAsync(id1, Arg.Any<CancellationToken>()).Returns(case1);
        _repository.GetByIdAsync(id2, Arg.Any<CancellationToken>()).Returns(case2);
        _repository.GetGroupedYearlyTrendAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<int>>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int, int)> { (2023, 10) });
        _llm.GenerateAnalysisAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("分析");

        await _handler.HandleAsync(new GetPopupAiAnalysisQuery(id1));
        await _handler.HandleAsync(new GetPopupAiAnalysisQuery(id2));

        // 內湖區+Car+16h → G4:C2:T5, 南港區+Bicycle+18h → G4:C2:T5 → same group
        await _llm.Received(1).GenerateAnalysisAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
