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

        SetupDefaultTrendMocks();

        _handler = new GetPopupAiAnalysisQueryHandler(
            _repository, _llm, _cache, _memoryCache,
            NullLogger<GetPopupAiAnalysisQueryHandler>.Instance);
    }

    private void SetupDefaultTrendMocks()
    {
        _repository.GetYearlyTrendByDimensionAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<int>>(),
            Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<(string, int, int)>() as IReadOnlyList<(string, int, int)>);

        _repository.GetGroupedYearlyTrendAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<int>>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<(int, int)>() as IReadOnlyList<(int, int)>);
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
    public async Task HandleAsync_ValidCase_CallsLlm()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(CreateCase(id, "內湖區", CaseType.Motorcycle, 18));

        _repository.GetYearlyTrendByDimensionAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<int>>(),
            16, 20, "caseType", Arg.Any<CancellationToken>())
            .Returns(new List<(string, int, int)> { ("機車竊盜", 2023, 50) });

        _repository.GetYearlyTrendByDimensionAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<int>>(),
            16, 20, "district", Arg.Any<CancellationToken>())
            .Returns(new List<(string, int, int)> { ("內湖區", 2023, 30) });

        _repository.GetYearlyTrendByDimensionAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<int>>(),
            null, null, "districtCaseType", Arg.Any<CancellationToken>())
            .Returns(new List<(string, int, int)> { ("內湖區-機車竊盜", 2023, 80) });

        _llm.GenerateAnalysisAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("分析結果");

        var result = await _handler.HandleAsync(new GetPopupAiAnalysisQuery(id));

        result.Should().Be("分析結果");
        await _llm.Received(1).GenerateAnalysisAsync(
            Arg.Is<string>(p => p.Contains("案類別") && p.Contains("行政區別") && p.Contains("不分時段")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_LlmFails_ReturnsFallback()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(CreateCase(id, "信義區", CaseType.Residential, 14));

        _repository.GetYearlyTrendByDimensionAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<int>>(),
            12, 16, "caseType", Arg.Any<CancellationToken>())
            .Returns(new List<(string, int, int)> { ("住宅竊盜", 2023, 30) });

        _llm.GenerateAnalysisAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new HttpRequestException("fail"));

        var result = await _handler.HandleAsync(new GetPopupAiAnalysisQuery(id));
        result.Should().Contain("AI 分析暫時無法使用");
    }

    [Fact]
    public async Task HandleAsync_SameGroup_SecondCallHitsCache()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(CreateCase(id, "內湖區", CaseType.Car, 16));

        _repository.GetYearlyTrendByDimensionAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<int>>(),
            Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<(string, int, int)> { ("汽車竊盜", 2023, 10) });

        _llm.GenerateAnalysisAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("cached");

        await _handler.HandleAsync(new GetPopupAiAnalysisQuery(id));
        await _handler.HandleAsync(new GetPopupAiAnalysisQuery(id));

        await _llm.Received(1).GenerateAnalysisAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── BuildPrompt 格式驗證 ──

    [Fact]
    public void BuildPrompt_TimeSlotLabelOnlyInHeader_NotRepeatedPerLine()
    {
        var byCaseType = new List<(string, int, int)>
        {
            ("住宅竊盜", 2023, 10), ("住宅竊盜", 2024, 15),
            ("機車竊盜", 2023, 5), ("機車竊盜", 2024, 8),
        };
        var byDistrict = new List<(string, int, int)>
        {
            ("大安區", 2023, 8), ("大安區", 2024, 12),
        };
        var byDistCaseType = new List<(string, int, int)>
        {
            ("大安區-住宅竊盜", 2023, 6), ("大安區-住宅竊盜", 2024, 9),
        };

        var prompt = GetPopupAiAnalysisQueryHandler.BuildPrompt("12~16 時", byCaseType, byDistrict, byDistCaseType);

        prompt.Should().Contain("案類別．12~16 時段（轄區合計）:");
        prompt.Should().Contain("住宅竊盜[");
        prompt.Should().NotContain("12~16 時-住宅竊盜");
        prompt.Should().NotContain("12~16-住宅竊盜");
    }

    [Fact]
    public void BuildPrompt_ThirdBlockMarkedAsNoTimeSlot()
    {
        var prompt = GetPopupAiAnalysisQueryHandler.BuildPrompt(
            "08~12 時",
            new List<(string, int, int)> { ("住宅竊盜", 2023, 10) },
            new List<(string, int, int)> { ("大安區", 2023, 10) },
            new List<(string, int, int)> { ("大安區-住宅竊盜", 2023, 10) });

        prompt.Should().Contain("行政區．案類（不分時段）:");
    }

    [Fact]
    public void BuildPrompt_ArrayContainsOnlyNumbers_NoYearLabels()
    {
        var prompt = GetPopupAiAnalysisQueryHandler.BuildPrompt(
            "08~12 時",
            new List<(string, int, int)> { ("住宅竊盜", 2023, 10), ("住宅竊盜", 2024, 20) },
            new List<(string, int, int)>(),
            new List<(string, int, int)>());

        prompt.Should().Contain("住宅竊盜[10,20]");
        prompt.Should().Contain("年度:2023~2024");
        prompt.Should().NotContain("2023年");
    }

    [Fact]
    public void BuildPrompt_YearRangeAtTop()
    {
        var prompt = GetPopupAiAnalysisQueryHandler.BuildPrompt(
            "04~08 時",
            new List<(string, int, int)> { ("搶奪", 2015, 5), ("搶奪", 2026, 3) },
            new List<(string, int, int)>(),
            new List<(string, int, int)>());

        prompt.Should().Contain("年度:2015~2026");
    }

    [Fact]
    public void BuildPrompt_MissingYearsFillWithZero()
    {
        var prompt = GetPopupAiAnalysisQueryHandler.BuildPrompt(
            "08~12 時",
            new List<(string, int, int)> { ("住宅竊盜", 2020, 10), ("住宅竊盜", 2022, 30) },
            new List<(string, int, int)> { ("大安區", 2021, 5) },
            new List<(string, int, int)>());

        // years: 2020,2021,2022
        prompt.Should().Contain("住宅竊盜[10,0,30]");
        prompt.Should().Contain("大安區[0,5,0]");
    }
}
