using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Exceptions;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Handlers;

public class GetCrimesByFilterQueryHandler
{
    private readonly ICrimeRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GetCrimesByFilterQueryHandler> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public GetCrimesByFilterQueryHandler(
        ICrimeRepository repository,
        IDistributedCache cache,
        ILogger<GetCrimesByFilterQueryHandler> logger)
    {
        _repository = repository;
        _cache = cache;
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

        var cacheKey = $"crimes:filter:{query.CaseType}:{query.DistrictName}:{query.YearFrom}:{query.YearTo}:{query.RawTimeSlot}:{query.Page}:{query.PageSize}";

        byte[]? cachedBytes = null;
        try
        {
            cachedBytes = await _cache.GetAsync(cacheKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "快取讀取失敗，fallthrough 到 Repository：{CacheKey}", cacheKey);
        }

        if (cachedBytes is not null)
        {
            _logger.LogInformation("快取命中：{CacheKey}", cacheKey);
            return JsonSerializer.Deserialize<PagedResult<TheftCaseDto>>(cachedBytes)!;
        }

        _logger.LogInformation("查詢案件：{Query}", query);
        var (cases, total) = await _repository.GetPagedByFilterAsync(filter, query.Page, query.PageSize, cancellationToken);
        _logger.LogInformation("查詢完成，共 {Total} 筆，本頁 {Count} 筆", total, cases.Count);

        var items = cases.Select(MapToDto).ToList();
        var totalPages = query.PageSize > 0 ? (int)Math.Ceiling((double)total / query.PageSize) : 0;
        var result = new PagedResult<TheftCaseDto>(items, total, query.Page, query.PageSize, totalPages);

        try
        {
            await _cache.SetAsync(
                cacheKey,
                JsonSerializer.SerializeToUtf8Bytes(result),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "快取寫入失敗，忽略繼續：{CacheKey}", cacheKey);
        }

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
