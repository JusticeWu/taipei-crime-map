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
                var l2Text = System.Text.Encoding.UTF8.GetString(l2Bytes);
                try { _memoryCache.Set(cacheKey, l2Text, L1Duration); } catch { }
                return l2Text;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI analysis L2 快取讀取失敗：{Key}", cacheKey);
        }

        // DB + LLM
        var districts = AnalysisGrouping.GetDistrictsInGroup(dg);
        var caseTypes = AnalysisGrouping.GetCaseTypesInGroup(cg);
        var (minHour, maxHour) = AnalysisGrouping.GetTimeSlotRange(tg);

        var trend = await _repository.GetGroupedYearlyTrendAsync(districts, caseTypes, minHour, maxHour, cancellationToken);

        if (trend.Count == 0)
            return "此分組無歷年趨勢資料可供分析。";

        var trendText = string.Join("、", trend.Select(t => $"{t.Year}年{t.Count}件"));
        var districtLabel = AnalysisGrouping.GetDistrictGroupLabel(dg);
        var caseTypeLabel = AnalysisGrouping.GetCaseTypeGroupLabel(cg);
        var timeSlotLabel = AnalysisGrouping.GetTimeSlotGroupLabel(tg);

        var prompt = $"""
            你是台北市治安數據分析師。以下是「{districtLabel}」地區，
            「{caseTypeLabel}」案類，「{timeSlotLabel}」時段的歷年案件數量趨勢：
            {trendText}。
            請用14句左右的繁體中文白話文分析這個趨勢，內容可包含趨勢變化描述、
            可能的成因推測、給使用者的簡單提醒或建議。
            不要使用項目符號或編號，直接以段落方式呈現。
            """;

        string analysis;
        try
        {
            analysis = await _llmService.GenerateAnalysisAsync(prompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM 分析失敗，回傳趨勢摘要：{Key}", cacheKey);
            analysis = $"【趨勢摘要】{districtLabel}／{caseTypeLabel}／{timeSlotLabel}：{trendText}（AI 分析暫時無法使用）";
        }

        // Write cache
        try
        {
            await _distributedCache.SetAsync(cacheKey,
                System.Text.Encoding.UTF8.GetBytes(analysis),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = L2Duration },
                cancellationToken);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "AI analysis L2 快取寫入失敗：{Key}", cacheKey); }

        try { _memoryCache.Set(cacheKey, analysis, L1Duration); } catch { }

        return analysis;
    }
}
