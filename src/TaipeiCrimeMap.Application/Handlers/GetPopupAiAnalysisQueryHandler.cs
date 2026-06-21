using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.Services;

namespace TaipeiCrimeMap.Application.Handlers;

public class GetPopupAiAnalysisQueryHandler
{
    private readonly ICrimeRepository _repository;
    private readonly ILlmAnalysisService _llmService;
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<GetPopupAiAnalysisQueryHandler> _logger;

    private static readonly TimeSpan L1Duration = TimeSpan.FromHours(24);
    private static readonly TimeSpan L2Duration = TimeSpan.FromDays(180);

    public GetPopupAiAnalysisQueryHandler(
        ICrimeRepository repository,
        ILlmAnalysisService llmService,
        IDistributedCache distributedCache,
        IMemoryCache memoryCache,
        ILogger<GetPopupAiAnalysisQueryHandler> logger)
    {
        _repository = repository;
        _llmService = llmService;
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<string?> HandleAsync(GetPopupAiAnalysisQuery query, CancellationToken cancellationToken = default)
    {
        var theftCase = await _repository.GetByIdAsync(query.CaseId, cancellationToken);
        if (theftCase is null) return null;

        if (theftCase.District?.Name is null || !theftCase.CaseType.HasValue || theftCase.TimeSlot?.StartHour is null)
            return "此案件缺少行政區、案類或時段資訊，無法進行趨勢分析。";

        var dg = AnalysisGrouping.GetDistrictGroup(theftCase.District.Name);
        var cg = AnalysisGrouping.GetCaseTypeGroup(theftCase.CaseType.Value);
        var tg = AnalysisGrouping.GetTimeSlotGroup(theftCase.TimeSlot.StartHour.Value);

        var cacheKey = $"ai-analysis:{dg}:{cg}:{tg}";

        // L1
        if (_memoryCache.TryGetValue(cacheKey, out string? l1) && l1 is not null)
            return l1;

        // L2
        try
        {
            var l2Bytes = await _distributedCache.GetAsync(cacheKey, cancellationToken);
            if (l2Bytes is not null)
            {
                var l2Text = Encoding.UTF8.GetString(l2Bytes);
                try { _memoryCache.Set(cacheKey, l2Text, L1Duration); } catch { }
                return l2Text;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI analysis L2 快取讀取失敗：{Key}", cacheKey);
        }

        // DB queries (parallel)
        var districts = AnalysisGrouping.GetDistrictsInGroup(dg);
        var caseTypes = AnalysisGrouping.GetCaseTypesInGroup(cg);
        var (minHour, maxHour) = AnalysisGrouping.GetTimeSlotRange(tg);
        var timeSlotLabel = AnalysisGrouping.GetTimeSlotGroupLabel(tg);

        var byCaseTypeTask = _repository.GetYearlyTrendByDimensionAsync(
            districts, caseTypes, minHour, maxHour, "caseType", cancellationToken);
        var byDistrictTask = _repository.GetYearlyTrendByDimensionAsync(
            districts, caseTypes, minHour, maxHour, "district", cancellationToken);
        var byDistCaseTypeTask = _repository.GetYearlyTrendByDimensionAsync(
            districts, caseTypes, null, null, "districtCaseType", cancellationToken);

        await Task.WhenAll(byCaseTypeTask, byDistrictTask, byDistCaseTypeTask);

        var byCaseType = await byCaseTypeTask;
        var byDistrict = await byDistrictTask;
        var byDistCaseType = await byDistCaseTypeTask;

        if (byCaseType.Count == 0 && byDistrict.Count == 0)
            return "此分組無歷年趨勢資料可供分析。";

        var prompt = BuildPrompt(timeSlotLabel, byCaseType, byDistrict, byDistCaseType);

        _logger.LogInformation("AI analysis prompt length: {Len} chars for {Key}", prompt.Length, cacheKey);

        string analysis;
        try
        {
            analysis = await _llmService.GenerateAnalysisAsync(prompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM 分析失敗：{Key}", cacheKey);
            analysis = $"【趨勢摘要】{BuildFallbackSummary(timeSlotLabel, byCaseType)}（AI 分析暫時無法使用）";
        }

        // Write cache
        try
        {
            await _distributedCache.SetAsync(cacheKey,
                Encoding.UTF8.GetBytes(analysis),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = L2Duration },
                cancellationToken);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "AI analysis L2 快取寫入失敗：{Key}", cacheKey); }

        try { _memoryCache.Set(cacheKey, analysis, L1Duration); } catch { }

        return analysis;
    }

    public static string BuildPrompt(
        string timeSlotLabel,
        IReadOnlyList<(string Key, int Year, int Count)> byCaseType,
        IReadOnlyList<(string Key, int Year, int Count)> byDistrict,
        IReadOnlyList<(string Key, int Year, int Count)> byDistCaseType)
    {
        var allYears = byCaseType.Select(r => r.Year)
            .Concat(byDistrict.Select(r => r.Year))
            .Concat(byDistCaseType.Select(r => r.Year))
            .Distinct().OrderBy(y => y).ToList();

        if (allYears.Count == 0) return "無資料";

        var yearRange = $"{allYears.First()}~{allYears.Last()}";
        var sb = new StringBuilder();

        sb.AppendLine("你是台北市治安數據分析師。以下是分組歷年案件趨勢數據，請用14句左右的繁體中文白話文分析，");
        sb.AppendLine("內容可包含趨勢變化描述、可能的成因推測、給使用者的簡單提醒或建議。");
        sb.AppendLine("不要使用項目符號或編號，直接以段落方式呈現。");
        sb.AppendLine();
        sb.AppendLine($"年度:{yearRange}");

        // Block 1: 案類別×時段
        sb.AppendLine($"案類別．{timeSlotLabel}段（轄區合計）:");
        AppendDimensionBlock(sb, byCaseType, allYears);

        // Block 2: 行政區別×時段
        sb.AppendLine();
        sb.AppendLine($"行政區別．{timeSlotLabel}段（案類合計）:");
        AppendDimensionBlock(sb, byDistrict, allYears);

        // Block 3: 行政區×案類（不分時段）
        sb.AppendLine();
        sb.AppendLine("行政區．案類（不分時段）:");
        AppendDimensionBlock(sb, byDistCaseType, allYears);

        return sb.ToString();
    }

    private static void AppendDimensionBlock(
        StringBuilder sb,
        IReadOnlyList<(string Key, int Year, int Count)> data,
        IReadOnlyList<int> allYears)
    {
        var grouped = data.GroupBy(r => r.Key).OrderByDescending(g => g.Sum(r => r.Count));
        foreach (var g in grouped)
        {
            var yearMap = g.ToDictionary(r => r.Year, r => r.Count);
            var counts = string.Join(",", allYears.Select(y => yearMap.TryGetValue(y, out var c) ? c.ToString() : "0"));
            sb.AppendLine($"{g.Key}[{counts}]");
        }
    }

    private static string BuildFallbackSummary(
        string timeSlotLabel,
        IReadOnlyList<(string Key, int Year, int Count)> byCaseType)
    {
        var grouped = byCaseType.GroupBy(r => r.Key);
        var parts = grouped.Select(g =>
        {
            var trend = string.Join("、", g.OrderBy(r => r.Year).Select(r => $"{r.Year}年{r.Count}件"));
            return $"{g.Key}：{trend}";
        });
        return $"{timeSlotLabel}／" + string.Join("；", parts);
    }
}
