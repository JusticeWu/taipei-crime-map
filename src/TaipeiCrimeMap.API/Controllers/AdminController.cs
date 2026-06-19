using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TaipeiCrimeMap.Application.Commands;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;
using TaipeiCrimeMap.Infrastructure.Geocoding;
using TaipeiCrimeMap.Infrastructure.Jobs;

namespace TaipeiCrimeMap.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("admin-api")]
public class AdminController : ControllerBase
{
    private readonly IMemoryCache _memoryCache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ICrimeRepository _repository;
    private readonly ICaseImportJobStore _jobStore;
    private readonly GeocodeMissingCommandHandler _geocodeMissingHandler;
    private readonly GoogleMapsOptions _googleMapsOptions;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IMemoryCache memoryCache,
        IConnectionMultiplexer redis,
        ICrimeRepository repository,
        ICaseImportJobStore jobStore,
        GeocodeMissingCommandHandler geocodeMissingHandler,
        IOptions<GoogleMapsOptions> googleMapsOptions,
        ILogger<AdminController> logger)
    {
        _memoryCache = memoryCache;
        _redis = redis;
        _repository = repository;
        _jobStore = jobStore;
        _geocodeMissingHandler = geocodeMissingHandler;
        _googleMapsOptions = googleMapsOptions.Value;
        _logger = logger;
    }

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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkAddCases(
        [FromBody] List<BulkCaseItem> items,
        CancellationToken cancellationToken)
    {
        if (items is null || items.Count == 0)
            return BadRequest("至少需要一筆資料。");

        try
        {
            var batchId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var jobs = items.Select(item => new CaseImportJob
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                CaseNumber = item.CaseNumber,
                CaseType = item.CaseType,
                OccurrenceDate = item.OccurrenceDate,
                TimeSlot = item.TimeSlot,
                RawLocation = item.RawLocation,
                Status = CaseImportJobStatus.Pending,
                NextRetryAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            }).ToList();

            await _jobStore.EnqueueBatchAsync(jobs, cancellationToken);

            return Ok(new BulkEnqueueResult(batchId, jobs.Count, "async"));
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Garnet 不可用，fallback 到同步寫入 DB");
            return Ok(await BulkAddSync(items, cancellationToken));
        }
    }

    private async Task<BulkSyncResult> BulkAddSync(List<BulkCaseItem> items, CancellationToken ct)
    {
        var succeeded = 0;
        var failures = new List<BulkSyncFailure>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            try
            {
                var caseType = CaseTypeExtensions.FromChineseName(item.CaseType)
                    ?? throw new ArgumentException($"無法對應案類「{item.CaseType}」");

                var dateStr = item.OccurrenceDate.ToString();
                if (dateStr.Length != 7)
                    throw new ArgumentException($"日期格式錯誤：{item.OccurrenceDate}，需為 7 碼民國年月日");

                var taiwanDate = TaiwanDate.Parse(dateStr);
                var timeSlot = TimeSlot.Parse(item.TimeSlot ?? string.Empty);
                var district = District.ParseFrom(item.RawLocation ?? string.Empty);

                var theftCase = TheftCase.Create(
                    caseNumber: item.CaseNumber,
                    caseType: caseType,
                    district: district,
                    occurredDate: taiwanDate,
                    timeSlot: timeSlot,
                    rawLocation: item.RawLocation ?? string.Empty);

                await _repository.AddAsync(theftCase, ct);

                var coord = await _repository.FindCoordinateByRawLocationAsync(
                    item.RawLocation ?? string.Empty, ct);
                if (coord is not null)
                    await _repository.UpdateCoordinateAsync(theftCase.Id, coord, ct);

                succeeded++;
            }
            catch (Exception ex)
            {
                failures.Add(new BulkSyncFailure(i, item.CaseNumber, ex.Message));
            }
        }

        return new BulkSyncResult(Guid.Empty, items.Count, succeeded, failures.Count, "sync", failures);
    }

    [HttpGet("cases/worker/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetWorkerStatus([FromServices] AdaptiveConcurrencyController? concurrency)
    {
        return Ok(new
        {
            currentConcurrencyLimit = concurrency?.CurrentLimit ?? 0,
            minLimit = concurrency?.MinLimit ?? 0,
            maxLimit = concurrency?.MaxLimit ?? 0,
        });
    }

    [HttpGet("cases/batch/{batchId:guid}/status")]
    [ProducesResponseType(typeof(BatchStatusResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatchStatus(Guid batchId, CancellationToken cancellationToken)
    {
        var jobs = await _jobStore.GetJobsByBatchIdAsync(batchId, cancellationToken);

        var pending = jobs.Count(j => j.Status == CaseImportJobStatus.Pending);
        var success = jobs.Count(j => j.Status == CaseImportJobStatus.Success);
        var failure = jobs.Count(j => j.Status == CaseImportJobStatus.Failure);
        var failures = jobs
            .Where(j => j.Status == CaseImportJobStatus.Failure)
            .Select(j => new BatchJobFailure(j.Id, j.CaseNumber, j.LastError ?? "未知錯誤"))
            .ToList();

        return Ok(new BatchStatusResult(pending, success, failure, failures));
    }

    [HttpPatch("cases/{caseNumber:int}/{caseType:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCase(
        int caseNumber,
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

    [HttpGet("cases/missing-coordinates")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMissingCoordinates(CancellationToken cancellationToken)
    {
        var cases = await _repository.GetCasesWithMissingCoordinatesAsync(int.MaxValue, cancellationToken);
        var items = cases.Select(c => new MissingCoordinatesCaseDto
        {
            Id = c.Id,
            CaseType = c.CaseType?.ToChineseName(),
            CaseNumber = c.CaseNumber,
            RawLocation = c.RawLocation,
        }).ToList();

        return Ok(new { totalCount = items.Count, items });
    }

    [HttpPost("cases/geocode-missing")]
    [ProducesResponseType(typeof(GeocodeMissingResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GeocodeMissing(
        [FromQuery] int? maxCount,
        CancellationToken cancellationToken)
    {
        var command = new GeocodeMissingCommand(maxCount, _googleMapsOptions.BatchDelayMs);
        var result = await _geocodeMissingHandler.HandleAsync(command, cancellationToken);
        return Ok(result);
    }

    public record BulkCaseItem(int CaseNumber, string CaseType, int OccurrenceDate, string? TimeSlot, string? RawLocation);
    public record BulkEnqueueResult(Guid BatchId, int TotalCount, string Mode);
    public record BulkSyncResult(Guid BatchId, int TotalCount, int SuccessCount, int FailureCount, string Mode, List<BulkSyncFailure> Failures);
    public record BulkSyncFailure(int Index, int CaseNumber, string Reason);
    public record BatchStatusResult(int Pending, int Success, int Failure, List<BatchJobFailure> Failures);
    public record BatchJobFailure(Guid JobId, int CaseNumber, string Error);
    public record UpdateCaseRequest(int? OccurrenceDate, string? TimeSlot);
}
