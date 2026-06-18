using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IMemoryCache _memoryCache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ICrimeRepository _repository;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IMemoryCache memoryCache,
        IConnectionMultiplexer redis,
        ICrimeRepository repository,
        ILogger<AdminController> logger)
    {
        _memoryCache = memoryCache;
        _redis = redis;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 清除所有快取：L1（IMemoryCache）+ L2（Garnet/Redis，FLUSHALL）（管理用途，需 Basic Authentication）
    /// </summary>
    [HttpPost("cache/clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ClearCache(CancellationToken cancellationToken)
    {
        var l1Cleared = false;
        if (_memoryCache is MemoryCache memoryCache)
        {
            memoryCache.Clear();
            l1Cleared = true;
        }

        var l2Cleared = false;
        try
        {
            var database = _redis.GetDatabase();
            await database.ExecuteAsync("FLUSHALL");
            l2Cleared = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清除 L2（Garnet/Redis）快取失敗");
        }

        return Ok(new { l1Cleared, l2Cleared });
    }

    [HttpPost("cases/bulk")]
    [ProducesResponseType(typeof(BulkAddResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkAddCases(
        [FromBody] List<BulkCaseItem> items,
        CancellationToken cancellationToken)
    {
        if (items is null || items.Count == 0)
            return BadRequest("至少需要一筆資料。");

        var succeeded = 0;
        var failures = new List<BulkFailure>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            try
            {
                var caseType = CaseTypeExtensions.FromChineseName(item.CaseType);
                if (caseType is null)
                {
                    failures.Add(new BulkFailure(i, item.CaseNumber, $"無法對應案類「{item.CaseType}」"));
                    continue;
                }

                var dateStr = item.OccurrenceDate.ToString();
                if (dateStr.Length != 7)
                {
                    failures.Add(new BulkFailure(i, item.CaseNumber, $"日期格式錯誤：{item.OccurrenceDate}，需為 7 碼民國年月日"));
                    continue;
                }

                var taiwanDate = TaiwanDate.Parse(dateStr);
                var timeSlot = TimeSlot.Parse(item.TimeSlot ?? string.Empty);
                var district = District.ParseFrom(item.RawLocation ?? string.Empty);

                var theftCase = TheftCase.Create(
                    caseNumber: item.CaseNumber.ToString(),
                    caseType: caseType,
                    district: district,
                    occurredDate: taiwanDate,
                    timeSlot: timeSlot,
                    rawLocation: item.RawLocation ?? string.Empty);

                await _repository.AddAsync(theftCase, cancellationToken);

                var existingCoord = await _repository.FindCoordinateByRawLocationAsync(
                    item.RawLocation ?? string.Empty, cancellationToken);
                if (existingCoord is not null)
                    await _repository.UpdateCoordinateAsync(theftCase.Id, existingCoord, cancellationToken);

                succeeded++;
            }
            catch (Exception ex)
            {
                failures.Add(new BulkFailure(i, item.CaseNumber, ex.Message));
            }
        }

        return Ok(new BulkAddResult(succeeded, failures.Count, failures));
    }

    [HttpPatch("cases/{caseNumber}/{caseType:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCase(
        string caseNumber,
        int caseType,
        [FromBody] UpdateCaseRequest request,
        CancellationToken cancellationToken)
    {
        string? dateRaw = request.OccurrenceDate.HasValue
            ? request.OccurrenceDate.Value.ToString()
            : null;
        string? timeSlotRaw = request.TimeSlot;

        if (dateRaw is null && timeSlotRaw is null)
            return BadRequest("至少需要提供 occurrenceDate 或 timeSlot。");

        if (dateRaw is not null && dateRaw.Length != 7)
            return BadRequest($"日期格式錯誤：{dateRaw}，需為 7 碼民國年月日。");

        var affected = await _repository.UpdateCaseFieldsAsync(
            caseNumber, caseType, dateRaw, timeSlotRaw, cancellationToken);

        return affected == 0 ? NotFound() : Ok(new { affected });
    }

    public record BulkCaseItem(int CaseNumber, string CaseType, int OccurrenceDate, string? TimeSlot, string? RawLocation);
    public record BulkFailure(int Index, int CaseNumber, string Reason);
    public record BulkAddResult(int Succeeded, int Failed, List<BulkFailure> Failures);
    public record UpdateCaseRequest(int? OccurrenceDate, string? TimeSlot);
}
