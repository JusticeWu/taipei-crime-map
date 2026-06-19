using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Exceptions;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Infrastructure.Jobs;

public sealed class CaseImportWorker : IHostedService, IDisposable
{
    private static readonly int[] BackoffSeconds = [2, 5, 12, 28];
    private const int MaxRetries = 5;

    private readonly ICaseImportJobStore _jobStore;
    private readonly ICrimeRepository _repository;
    private readonly ILogger<CaseImportWorker> _logger;
    private readonly int _batchSize;
    private readonly int _maxConcurrency;
    private CancellationTokenSource? _cts;
    private Task? _executeTask;

    public CaseImportWorker(
        ICaseImportJobStore jobStore,
        ICrimeRepository repository,
        IConfiguration configuration,
        ILogger<CaseImportWorker> logger)
    {
        _jobStore = jobStore;
        _repository = repository;
        _logger = logger;
        _batchSize = configuration.GetValue("CaseImportWorker:BatchSize", 50);
        _maxConcurrency = configuration.GetValue("CaseImportWorker:MaxConcurrency", 5);
    }

    public int BatchSize => _batchSize;
    public int MaxConcurrency => _maxConcurrency;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CaseImportWorker 已啟動，BatchSize={BatchSize}，MaxConcurrency={MaxConcurrency}", _batchSize, _maxConcurrency);
        _cts = new CancellationTokenSource();
        _executeTask = ExecuteLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var cts = _cts;
        _cts = null;
        if (cts is not null)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
        if (_executeTask is not null)
        {
            try { await _executeTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }

    private async Task ExecuteLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var jobs = await _jobStore.GetPendingJobsAsync(DateTimeOffset.UtcNow, _batchSize, ct);
                processed = jobs.Count;

                if (processed > 0)
                {
                    using var semaphore = new SemaphoreSlim(_maxConcurrency);
                    var tasks = jobs.Select(async job =>
                    {
                        await semaphore.WaitAsync(ct);
                        try { await ProcessJobAsync(job, ct); }
                        finally { semaphore.Release(); }
                    });
                    await Task.WhenAll(tasks);
                }

                sw.Stop();
                _logger.LogDebug("本輪處理 {Count} 筆，耗時 {Ms}ms，{Action}",
                    processed, sw.ElapsedMilliseconds,
                    processed > 0 ? "立即處理下一批" : "等待 3 秒");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CaseImportWorker 主迴圈例外");
            }

            if (processed == 0)
            {
                try { await Task.Delay(3000, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task ProcessJobAsync(CaseImportJob job, CancellationToken ct)
    {
        try
        {
            var caseType = CaseTypeExtensions.FromChineseName(job.CaseType)
                ?? throw new DomainException($"無法對應案類「{job.CaseType}」");

            var dateStr = job.OccurrenceDate.ToString();
            if (dateStr.Length != 7)
                throw new DomainException($"日期格式錯誤：{job.OccurrenceDate}，需為 7 碼民國年月日");

            var taiwanDate = TaiwanDate.Parse(dateStr);
            var timeSlot = TimeSlot.Parse(job.TimeSlot ?? string.Empty);
            var district = District.ParseFrom(job.RawLocation ?? string.Empty);

            var theftCase = TheftCase.Create(
                caseNumber: job.CaseNumber,
                caseType: caseType,
                district: district,
                occurredDate: taiwanDate,
                timeSlot: timeSlot,
                rawLocation: job.RawLocation ?? string.Empty);

            await _repository.AddAsync(theftCase, ct);

            var existingCoord = await _repository.FindCoordinateByRawLocationAsync(
                job.RawLocation ?? string.Empty, ct);
            if (existingCoord is not null)
                await _repository.UpdateCoordinateAsync(theftCase.Id, existingCoord, ct);

            job.Status = CaseImportJobStatus.Success;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await _jobStore.UpdateJobAsync(job, ct);

            _logger.LogDebug("Job {JobId} 處理成功，caseNumber={CaseNumber}", job.Id, job.CaseNumber);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            job.UpdatedAt = DateTimeOffset.UtcNow;

            if (IsTransientError(ex))
            {
                job.RetryCount++;
                if (job.RetryCount >= MaxRetries)
                {
                    job.Status = CaseImportJobStatus.Failure;
                    job.LastError = $"超過最大重試次數({MaxRetries})：{ex.Message}";
                }
                else
                {
                    job.NextRetryAt = CalculateNextRetry(job.RetryCount, DateTimeOffset.UtcNow);
                    job.LastError = ex.Message;
                }
            }
            else
            {
                job.Status = CaseImportJobStatus.Failure;
                job.LastError = ex.Message;
            }

            try { await _jobStore.UpdateJobAsync(job, ct); }
            catch (Exception storeEx)
            {
                _logger.LogWarning(storeEx, "更新 job {JobId} 狀態失敗", job.Id);
            }

            _logger.LogDebug("Job {JobId} 處理失敗：{Error}，status={Status}，retryCount={RetryCount}",
                job.Id, ex.Message, job.Status, job.RetryCount);
        }
    }

    public static DateTimeOffset CalculateNextRetry(int retryCount, DateTimeOffset baseTime)
    {
        var index = Math.Clamp(retryCount - 1, 0, BackoffSeconds.Length - 1);
        return baseTime.AddSeconds(BackoffSeconds[index]);
    }

    public static bool IsTransientError(Exception ex) => ex switch
    {
        SqlException sqlEx => sqlEx.Number == -2 || sqlEx.Class >= 20,
        RedisConnectionException => true,
        TimeoutException => true,
        _ => false,
    };
}
