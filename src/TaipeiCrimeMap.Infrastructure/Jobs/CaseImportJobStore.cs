using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace TaipeiCrimeMap.Infrastructure.Jobs;

public interface ICaseImportJobStore
{
    Task EnqueueBatchAsync(IReadOnlyList<CaseImportJob> jobs, CancellationToken ct = default);
    Task<IReadOnlyList<CaseImportJob>> GetPendingJobsAsync(DateTimeOffset asOf, int count = int.MaxValue, CancellationToken ct = default);
    Task UpdateJobAsync(CaseImportJob job, CancellationToken ct = default);
    Task<IReadOnlyList<CaseImportJob>> GetJobsByBatchIdAsync(Guid batchId, CancellationToken ct = default);
}

public sealed class CaseImportJobStore : ICaseImportJobStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly TimeSpan KeyTtl = TimeSpan.FromDays(7);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CaseImportJobStore> _logger;

    public CaseImportJobStore(IConnectionMultiplexer redis, ILogger<CaseImportJobStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task EnqueueBatchAsync(IReadOnlyList<CaseImportJob> jobs, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var batch = db.CreateBatch();
        var tasks = new List<Task>();

        foreach (var job in jobs)
        {
            var jobKey = JobKey(job.Id);
            var json = JsonSerializer.Serialize(job, JsonOptions);
            var score = job.NextRetryAt.ToUnixTimeMilliseconds();

            tasks.Add(batch.StringSetAsync(jobKey, json));
            tasks.Add(batch.KeyExpireAsync(jobKey, KeyTtl));
            tasks.Add(batch.SetAddAsync(BatchKey(job.BatchId), job.Id.ToString()));
            tasks.Add(batch.SortedSetAddAsync("pending-jobs", job.Id.ToString(), score));
        }

        if (jobs.Count > 0)
        {
            tasks.Add(batch.KeyExpireAsync(BatchKey(jobs[0].BatchId), KeyTtl));
        }

        batch.Execute();
        await Task.WhenAll(tasks);
        _logger.LogDebug("已排入 {Count} 筆 job，batchId: {BatchId}", jobs.Count, jobs.Count > 0 ? jobs[0].BatchId : Guid.Empty);
    }

    public async Task<IReadOnlyList<CaseImportJob>> GetPendingJobsAsync(DateTimeOffset asOf, int count = int.MaxValue, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var members = await db.SortedSetRangeByScoreAsync("pending-jobs", stop: asOf.ToUnixTimeMilliseconds(), take: count);

        if (members.Length == 0) return [];

        var result = new List<CaseImportJob>();
        foreach (var member in members)
        {
            var json = await db.StringGetAsync(JobKey(member!));
            if (json.IsNullOrEmpty) continue;
            var job = JsonSerializer.Deserialize<CaseImportJob>(json!, JsonOptions);
            if (job is not null && job.Status == CaseImportJobStatus.Pending)
                result.Add(job);
        }
        return result;
    }

    public async Task UpdateJobAsync(CaseImportJob job, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var jobKey = JobKey(job.Id);
        var json = JsonSerializer.Serialize(job, JsonOptions);

        await db.StringSetAsync(jobKey, json);
        await db.KeyExpireAsync(jobKey, KeyTtl);

        if (job.Status is CaseImportJobStatus.Success or CaseImportJobStatus.Failure)
            await db.SortedSetRemoveAsync("pending-jobs", job.Id.ToString());
        else
            await db.SortedSetAddAsync("pending-jobs", job.Id.ToString(), job.NextRetryAt.ToUnixTimeMilliseconds());
    }

    public async Task<IReadOnlyList<CaseImportJob>> GetJobsByBatchIdAsync(Guid batchId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var members = await db.SetMembersAsync(BatchKey(batchId));

        if (members.Length == 0) return [];

        var result = new List<CaseImportJob>();
        foreach (var member in members)
        {
            var json = await db.StringGetAsync(JobKey(member!));
            if (json.IsNullOrEmpty) continue;
            var job = JsonSerializer.Deserialize<CaseImportJob>(json!, JsonOptions);
            if (job is not null) result.Add(job);
        }
        return result;
    }

    private static string JobKey(Guid id) => $"case-job:{id}";
    private static string JobKey(RedisValue id) => $"case-job:{id}";
    private static string BatchKey(Guid batchId) => $"batch:{batchId}:jobs";
}
