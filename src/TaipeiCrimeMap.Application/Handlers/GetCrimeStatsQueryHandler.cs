using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Handlers;

public class GetCrimeStatsQueryHandler
{
    private readonly ICrimeRepository _repository;
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<GetCrimeStatsQueryHandler> _logger;

    private static readonly TimeSpan L1Duration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan L2Duration = TimeSpan.FromMinutes(30);

    public GetCrimeStatsQueryHandler(
        ICrimeRepository repository,
        IDistributedCache distributedCache,
        IMemoryCache memoryCache,
        ILogger<GetCrimeStatsQueryHandler> logger)
    {
        _repository = repository;
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<CrimeStatsDto> HandleAsync(
        GetCrimeStatsQuery query,
        CancellationToken cancellationToken = default)
    {
        var district = string.IsNullOrWhiteSpace(query.DistrictName)
            ? null
            : District.ParseFrom(query.DistrictName);

        var filter = new CrimeFilter(
            caseType: query.CaseType,
            district: district,
            yearFrom: query.YearFrom,
            yearTo: query.YearTo);

        var cacheKey = $"crimes:stats:{query.CaseType}:{query.DistrictName}:{query.YearFrom}:{query.YearTo}";

        // L1: MemoryCache（1 分鐘）
        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out CrimeStatsDto? l1Result) && l1Result is not null)
            {
                _logger.LogInformation("L1 快取命中：{CacheKey}", cacheKey);
                return l1Result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "L1 快取讀取失敗，繼續往下：{CacheKey}", cacheKey);
        }

        // L2: Garnet / DistributedCache（30 分鐘）
        byte[]? cachedBytes = null;
        try
        {
            cachedBytes = await _distributedCache.GetAsync(cacheKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "L2 快取讀取失敗，繼續往下：{CacheKey}", cacheKey);
        }

        if (cachedBytes is not null)
        {
            _logger.LogInformation("L2 快取命中：{CacheKey}", cacheKey);
            var l2Result = JsonSerializer.Deserialize<CrimeStatsDto>(cachedBytes)!;
            try { _memoryCache.Set(cacheKey, l2Result, L1Duration); }
            catch (Exception ex) { _logger.LogWarning(ex, "L1 回填失敗：{CacheKey}", cacheKey); }
            return l2Result;
        }

        // DB
        var (districtCounts, timeSlotCounts) = await _repository.GetStatsByFilterAsync(filter, cancellationToken);

        var result = new CrimeStatsDto(
            DistrictDistribution: districtCounts
                .OrderByDescending(c => c.Count)
                .Select(c => new DistrictDistributionDto(c.District, c.Count))
                .ToList(),
            TimeSlotDistribution: timeSlotCounts
                .OrderBy(c => c.TimeSlot, StringComparer.Ordinal)
                .Select(c => new TimeSlotDistributionDto(c.TimeSlot, c.Count))
                .ToList());

        // 寫入 L2
        try
        {
            await _distributedCache.SetAsync(
                cacheKey,
                JsonSerializer.SerializeToUtf8Bytes(result),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = L2Duration },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "L2 快取寫入失敗：{CacheKey}", cacheKey);
        }

        // 寫入 L1
        try { _memoryCache.Set(cacheKey, result, L1Duration); }
        catch (Exception ex) { _logger.LogWarning(ex, "L1 快取寫入失敗：{CacheKey}", cacheKey); }

        return result;
    }
}
