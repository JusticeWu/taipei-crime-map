using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TaipeiCrimeMap.Domain.Repositories;
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
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IMemoryCache memoryCache,
        IConnectionMultiplexer redis,
        ICrimeRepository repository,
        ICaseImportJobStore jobStore,
        ILogger<AdminController> logger)
    {
        _memoryCache = memoryCache;
        _redis = redis;
        _repository = repository;
        _jobStore = jobStore;
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
    [ProducesResponseType(typeof(BulkEnqueueResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkAddCases(
        [FromBody] List<BulkCaseItem> items,
        CancellationToken cancellationToken)
    {
        if (items is null || items.Count == 0)
            return BadRequest("至少需要一筆資料。");

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

        return Ok(new BulkEnqueueResult(batchId, jobs.Count));
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

    public record BulkCaseItem(int CaseNumber, string CaseType, int OccurrenceDate, string? TimeSlot, string? RawLocation);
    public record BulkEnqueueResult(Guid BatchId, int TotalCount);
    public record BatchStatusResult(int Pending, int Success, int Failure, List<BatchJobFailure> Failures);
    public record BatchJobFailure(Guid JobId, int CaseNumber, string Error);
    public record UpdateCaseRequest(int? OccurrenceDate, string? TimeSlot);
}
