using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Interfaces;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Exceptions;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Handlers;

public class GetCrimesByFilterQueryHandler
{
    private readonly ICrimeRepository _repository;
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ITimingTracker _timing;
    private readonly ILogger<GetCrimesByFilterQueryHandler> _logger;

    private static readonly TimeSpan L1Duration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan L2Duration = TimeSpan.FromMinutes(30);

    public GetCrimesByFilterQueryHandler(
        ICrimeRepository repository,
        IDistributedCache distributedCache,
        IMemoryCache memoryCache,
        ITimingTracker timing,
        ILogger<GetCrimesByFilterQueryHandler> logger)
    {
        _repository = repository;
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        _timing = timing;
        _logger = logger;
    }

    public async Task<PagedResult<TheftCaseDto>> HandleAsync(
        GetCrimesByFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var district = string.IsNullOrWhiteSpace(query.DistrictName)
            ? null
            : District.ParseFrom(query.DistrictName);

        var timeSlot = string.IsNullOrWhiteSpace(query.RawTimeSlot)
            ? null
            : TimeSlot.Parse(query.RawTimeSlot);

        if (!string.IsNullOrWhiteSpace(query.RawTimeSlot) && timeSlot?.StartHour is null)
            throw new DomainException($"時段格式不正確：{query.RawTimeSlot}");

        var filter = new CrimeFilter(
            caseType: query.CaseType,
            district: district,
            yearFrom: query.YearFrom,
            yearTo: query.YearTo,
            timeSlot: timeSlot);

        var cacheKey = $"crimes:filter:{query.CaseType}:{query.DistrictName}:{query.YearFrom}:{query.YearTo}:{query.RawTimeSlot}:{query.Page}:{query.PageSize}:{query.SortBy}:{query.SortOrder}";

        // L1: MemoryCache（1 分鐘）
        // 注意：LogSummary() 必須在 using 區塊結束（StageTimer.Dispose 寫入 _records）之後才能呼叫，
        // 否則命中時 _records 還是空的，LogSummary 會直接略過不輸出任何內容
        PagedResult<TheftCaseDto>? l1Result = null;
        using (_timing.Track("L1-Cache"))
        {
            try
            {
                if (_memoryCache.TryGetValue(cacheKey, out PagedResult<TheftCaseDto>? cached) && cached is not null)
                {
                    l1Result = cached;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "L1 快取讀取失敗，繼續往下：{CacheKey}", cacheKey);
            }
        }

        if (l1Result is not null)
        {
            _logger.LogInformation("L1 快取命中：{CacheKey}", cacheKey);
            _timing.LogSummary();
            return l1Result;
        }

        // L2: Garnet / DistributedCache（30 分鐘）
        byte[]? cachedBytes = null;
        using (_timing.Track("L2-Cache"))
        {
            try
            {
                cachedBytes = await _distributedCache.GetAsync(cacheKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "L2 快取讀取失敗，繼續往下：{CacheKey}", cacheKey);
            }
        }

        if (cachedBytes is not null)
        {
            _logger.LogInformation("L2 快取命中：{CacheKey}", cacheKey);
            var l2Result = JsonSerializer.Deserialize<PagedResult<TheftCaseDto>>(cachedBytes)!;
            try { _memoryCache.Set(cacheKey, l2Result, L1Duration); }
            catch (Exception ex) { _logger.LogWarning(ex, "L1 回填失敗：{CacheKey}", cacheKey); }
            _timing.LogSummary();
            return l2Result;
        }

        // DB
        _logger.LogInformation("查詢案件：{Query}", query);
        IReadOnlyList<TheftCase> cases;
        int total;
        using (_timing.Track("DB-Query"))
        {
            (cases, total) = await _repository.GetPagedByFilterAsync(filter, query.Page, query.PageSize, query.SortBy, query.SortOrder, cancellationToken);
        }
        _logger.LogInformation("查詢完成，共 {Total} 筆，本頁 {Count} 筆", total, cases.Count);

        List<TheftCaseDto> items;
        using (_timing.Track("DTO-Map"))
        {
            items = cases.Select(MapToDto).ToList();
        }
        var totalPages = query.PageSize > 0 ? (int)Math.Ceiling((double)total / query.PageSize) : 0;
        var result = new PagedResult<TheftCaseDto>(items, total, query.Page, query.PageSize, totalPages);

        // 寫入 L2
        using (_timing.Track("L2-Write"))
        {
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
        }

        // 寫入 L1
        using (_timing.Track("L1-Write"))
        {
            try { _memoryCache.Set(cacheKey, result, L1Duration); }
            catch (Exception ex) { _logger.LogWarning(ex, "L1 快取寫入失敗：{CacheKey}", cacheKey); }
        }

        _timing.LogSummary();
        return result;
    }

    private static TheftCaseDto MapToDto(TheftCase c) => new()
    {
        Id = c.Id,
        CaseNumber = c.CaseNumber,
        CaseType = c.CaseType?.ToChineseName(),
        District = c.District?.Name,
        OccurredDate = c.OccurredDate?.OccurredOn?.ToString("yyyy-MM-dd"),
        TimeSlot = c.TimeSlot?.Normalize(),
        RawLocation = c.RawLocation,
        Latitude = c.Coordinate?.Latitude,
        Longitude = c.Coordinate?.Longitude,
        IsDataComplete = c.IsDataComplete
    };
}
