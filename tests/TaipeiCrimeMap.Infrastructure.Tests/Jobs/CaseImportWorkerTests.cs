using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using TaipeiCrimeMap.Domain.Exceptions;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.Services;
using TaipeiCrimeMap.Domain.ValueObjects;
using TaipeiCrimeMap.Infrastructure.Jobs;


namespace TaipeiCrimeMap.Infrastructure.Tests.Jobs;

public class CaseImportWorkerTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 5)]
    [InlineData(3, 12)]
    [InlineData(4, 28)]
    public void CalculateNextRetry_ReturnsExpectedBackoff(int retryCount, int expectedSeconds)
    {
        var baseTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = CaseImportWorker.CalculateNextRetry(retryCount, baseTime);
        result.Should().Be(baseTime.AddSeconds(expectedSeconds));
    }

    [Fact]
    public void CalculateNextRetry_RetryCountBeyondArray_ClampedToMax()
    {
        var baseTime = DateTimeOffset.UtcNow;
        var result = CaseImportWorker.CalculateNextRetry(10, baseTime);
        result.Should().Be(baseTime.AddSeconds(28));
    }

    [Fact]
    public void IsTransientError_TimeoutException_ReturnsTrue()
    {
        CaseImportWorker.IsTransientError(new TimeoutException()).Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_RedisConnectionException_ReturnsTrue()
    {
        var ex = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "lost");
        CaseImportWorker.IsTransientError(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_DomainException_ReturnsFalse()
    {
        CaseImportWorker.IsTransientError(new DomainException("bad")).Should().BeFalse();
    }

    [Fact]
    public void IsTransientError_FormatException_ReturnsFalse()
    {
        CaseImportWorker.IsTransientError(new FormatException("bad")).Should().BeFalse();
    }

    [Fact]
    public void IsTransientError_ArgumentException_ReturnsFalse()
    {
        CaseImportWorker.IsTransientError(new ArgumentException("bad")).Should().BeFalse();
    }

    private static CaseImportWorker BuildWorker(
        Dictionary<string, string?>? settings = null,
        ICaseImportJobStore? jobStore = null,
        ICrimeRepository? repository = null,
        IGeocodingService? geocodingService = null)
    {
        var builder = new ConfigurationBuilder();
        if (settings is not null)
            builder.AddInMemoryCollection(settings);
        var config = builder.Build();
        var maxC = config.GetValue("CaseImportWorker:MaxConcurrency", 5);
        return new CaseImportWorker(
            jobStore ?? Substitute.For<ICaseImportJobStore>(),
            repository ?? Substitute.For<ICrimeRepository>(),
            geocodingService ?? Substitute.For<IGeocodingService>(),
            new AdaptiveConcurrencyController(maxC, 1, Math.Max(maxC * 2, 20)),
            config,
            NullLogger<CaseImportWorker>.Instance);
    }

    [Fact]
    public void Constructor_ReadsBatchSizeFromConfig()
    {
        var worker = BuildWorker(new() { ["CaseImportWorker:BatchSize"] = "25" });
        worker.BatchSize.Should().Be(25);
    }

    [Fact]
    public void Constructor_ReadsMaxConcurrencyFromConfig()
    {
        var worker = BuildWorker(new() { ["CaseImportWorker:MaxConcurrency"] = "1" });
        worker.MaxConcurrency.Should().Be(1);
    }

    [Fact]
    public void Constructor_DefaultValues_WhenConfigMissing()
    {
        var worker = BuildWorker();
        worker.BatchSize.Should().Be(50);
        worker.MaxConcurrency.Should().Be(5);
    }

    [Fact(Timeout = 15000)]
    public async Task ExecuteLoop_WithPendingJobs_ProcessesWithoutDelay()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var jobStore = Substitute.For<ICaseImportJobStore>();
        var callTimestamps = new List<long>();
        var done = new TaskCompletionSource();

        jobStore.GetPendingJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callTimestamps.Add(System.Diagnostics.Stopwatch.GetTimestamp());
                if (callTimestamps.Count <= 2)
                {
                    return (IReadOnlyList<CaseImportJob>)new List<CaseImportJob>
                    {
                        new() { Id = Guid.NewGuid(), CaseType = "住宅竊盜", OccurrenceDate = 1150329, RawLocation = "臺北市大安區" }
                    };
                }
                done.TrySetResult();
                return (IReadOnlyList<CaseImportJob>)Array.Empty<CaseImportJob>();
            });

        var config = new ConfigurationBuilder().Build();
        var cc = new AdaptiveConcurrencyController(5, 1, 20);
        var worker = new CaseImportWorker(jobStore, Substitute.For<ICrimeRepository>(), Substitute.For<IGeocodingService>(), cc, config, NullLogger<CaseImportWorker>.Instance);

        await worker.StartAsync(cts.Token);
        await Task.WhenAny(done.Task, Task.Delay(Timeout.Infinite, cts.Token));
        await worker.StopAsync(CancellationToken.None);

        callTimestamps.Count.Should().BeGreaterThanOrEqualTo(2);
        var elapsedMs = (callTimestamps[1] - callTimestamps[0]) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        elapsedMs.Should().BeLessThan(2000, "should not wait 3s between batches when jobs exist");
    }

    [Fact(Timeout = 15000)]
    public async Task ProcessJob_GeocodingSuccess_SetsHasCoordinateTrue()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var jobStore = Substitute.For<ICaseImportJobStore>();
        var repository = Substitute.For<ICrimeRepository>();
        var geocoding = Substitute.For<IGeocodingService>();
        var coord = GeoCoordinate.Create(25.03, 121.56);
        CaseImportJob? updatedJob = null;
        var done = new TaskCompletionSource();

        var job = new CaseImportJob
        {
            Id = Guid.NewGuid(),
            CaseType = "住宅竊盜",
            OccurrenceDate = 1150329,
            TimeSlot = "06~08",
            RawLocation = "臺北市大安區忠孝東路四段"
        };

        var callCount = 0;
        jobStore.GetPendingJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    return (IReadOnlyList<CaseImportJob>)new List<CaseImportJob> { job };
                done.TrySetResult();
                return (IReadOnlyList<CaseImportJob>)Array.Empty<CaseImportJob>();
            });

        repository.FindCoordinateByRawLocationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);
        geocoding.GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(coord);

        jobStore.UpdateJobAsync(Arg.Any<CaseImportJob>(), Arg.Any<CancellationToken>())
            .Returns(ci => { updatedJob = ci.Arg<CaseImportJob>(); return Task.CompletedTask; });

        var worker = BuildWorker(jobStore: jobStore, repository: repository, geocodingService: geocoding);
        await worker.StartAsync(cts.Token);
        await Task.WhenAny(done.Task, Task.Delay(Timeout.Infinite, cts.Token));
        await worker.StopAsync(CancellationToken.None);

        updatedJob.Should().NotBeNull();
        updatedJob!.Status.Should().Be(CaseImportJobStatus.Success);
        updatedJob.HasCoordinate.Should().BeTrue();
        updatedJob.LastError.Should().BeNull();

        await repository.Received(1).UpdateCoordinateAsync(Arg.Any<Guid>(), coord, Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 15000)]
    public async Task ProcessJob_GeocodingFails_SetsHasCoordinateFalseButStatusSuccess()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var jobStore = Substitute.For<ICaseImportJobStore>();
        var repository = Substitute.For<ICrimeRepository>();
        var geocoding = Substitute.For<IGeocodingService>();
        CaseImportJob? updatedJob = null;
        var done = new TaskCompletionSource();

        var job = new CaseImportJob
        {
            Id = Guid.NewGuid(),
            CaseType = "住宅竊盜",
            OccurrenceDate = 1150329,
            TimeSlot = "06~08",
            RawLocation = "無法定位的地址"
        };

        var callCount = 0;
        jobStore.GetPendingJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    return (IReadOnlyList<CaseImportJob>)new List<CaseImportJob> { job };
                done.TrySetResult();
                return (IReadOnlyList<CaseImportJob>)Array.Empty<CaseImportJob>();
            });

        repository.FindCoordinateByRawLocationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);
        geocoding.GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);

        jobStore.UpdateJobAsync(Arg.Any<CaseImportJob>(), Arg.Any<CancellationToken>())
            .Returns(ci => { updatedJob = ci.Arg<CaseImportJob>(); return Task.CompletedTask; });

        var worker = BuildWorker(jobStore: jobStore, repository: repository, geocodingService: geocoding);
        await worker.StartAsync(cts.Token);
        await Task.WhenAny(done.Task, Task.Delay(Timeout.Infinite, cts.Token));
        await worker.StopAsync(CancellationToken.None);

        updatedJob.Should().NotBeNull();
        updatedJob!.Status.Should().Be(CaseImportJobStatus.Success);
        updatedJob.HasCoordinate.Should().BeFalse();
        updatedJob.LastError.Should().Contain("座標查詢失敗");

        await repository.DidNotReceive().UpdateCoordinateAsync(Arg.Any<Guid>(), Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 15000)]
    public async Task ProcessJob_GeocodingThrows_SetsHasCoordinateFalseButStatusSuccess()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var jobStore = Substitute.For<ICaseImportJobStore>();
        var repository = Substitute.For<ICrimeRepository>();
        var geocoding = Substitute.For<IGeocodingService>();
        CaseImportJob? updatedJob = null;
        var done = new TaskCompletionSource();

        var job = new CaseImportJob
        {
            Id = Guid.NewGuid(),
            CaseType = "住宅竊盜",
            OccurrenceDate = 1150329,
            TimeSlot = "06~08",
            RawLocation = "臺北市大安區"
        };

        var callCount = 0;
        jobStore.GetPendingJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    return (IReadOnlyList<CaseImportJob>)new List<CaseImportJob> { job };
                done.TrySetResult();
                return (IReadOnlyList<CaseImportJob>)Array.Empty<CaseImportJob>();
            });

        repository.FindCoordinateByRawLocationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);
        geocoding.GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<GeoCoordinate?>(_ => throw new HttpRequestException("Google API 連線失敗"));

        jobStore.UpdateJobAsync(Arg.Any<CaseImportJob>(), Arg.Any<CancellationToken>())
            .Returns(ci => { updatedJob = ci.Arg<CaseImportJob>(); return Task.CompletedTask; });

        var worker = BuildWorker(jobStore: jobStore, repository: repository, geocodingService: geocoding);
        await worker.StartAsync(cts.Token);
        await Task.WhenAny(done.Task, Task.Delay(Timeout.Infinite, cts.Token));
        await worker.StopAsync(CancellationToken.None);

        updatedJob.Should().NotBeNull();
        updatedJob!.Status.Should().Be(CaseImportJobStatus.Success);
        updatedJob.HasCoordinate.Should().BeFalse();
        updatedJob.LastError.Should().Contain("座標查詢失敗");
        updatedJob.LastError.Should().Contain("Google API 連線失敗");
    }

    [Fact(Timeout = 15000)]
    public async Task ProcessJob_ExistingCoordinate_SetsHasCoordinateTrue_SkipsGeocoding()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var jobStore = Substitute.For<ICaseImportJobStore>();
        var repository = Substitute.For<ICrimeRepository>();
        var geocoding = Substitute.For<IGeocodingService>();
        var coord = GeoCoordinate.Create(25.03, 121.56);
        CaseImportJob? updatedJob = null;
        var done = new TaskCompletionSource();

        var job = new CaseImportJob
        {
            Id = Guid.NewGuid(),
            CaseType = "住宅竊盜",
            OccurrenceDate = 1150329,
            TimeSlot = "06~08",
            RawLocation = "臺北市大安區忠孝東路四段"
        };

        var callCount = 0;
        jobStore.GetPendingJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    return (IReadOnlyList<CaseImportJob>)new List<CaseImportJob> { job };
                done.TrySetResult();
                return (IReadOnlyList<CaseImportJob>)Array.Empty<CaseImportJob>();
            });

        repository.FindCoordinateByRawLocationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(coord);

        jobStore.UpdateJobAsync(Arg.Any<CaseImportJob>(), Arg.Any<CancellationToken>())
            .Returns(ci => { updatedJob = ci.Arg<CaseImportJob>(); return Task.CompletedTask; });

        var worker = BuildWorker(jobStore: jobStore, repository: repository, geocodingService: geocoding);
        await worker.StartAsync(cts.Token);
        await Task.WhenAny(done.Task, Task.Delay(Timeout.Infinite, cts.Token));
        await worker.StopAsync(CancellationToken.None);

        updatedJob.Should().NotBeNull();
        updatedJob!.Status.Should().Be(CaseImportJobStatus.Success);
        updatedJob.HasCoordinate.Should().BeTrue();

        await geocoding.DidNotReceive().GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
