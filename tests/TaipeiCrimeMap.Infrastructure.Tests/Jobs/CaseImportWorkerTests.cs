using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using TaipeiCrimeMap.Domain.Exceptions;
using TaipeiCrimeMap.Domain.Repositories;
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

    private static CaseImportWorker BuildWorker(Dictionary<string, string?>? settings = null)
    {
        var builder = new ConfigurationBuilder();
        if (settings is not null)
            builder.AddInMemoryCollection(settings);
        return new CaseImportWorker(
            Substitute.For<ICaseImportJobStore>(),
            Substitute.For<ICrimeRepository>(),
            builder.Build(),
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

    [Fact(Timeout = 10000)]
    public async Task ExecuteLoop_WithPendingJobs_ProcessesWithoutDelay()
    {
        var jobStore = Substitute.For<ICaseImportJobStore>();
        var callCount = 0;
        var callTimestamps = new List<long>();

        jobStore.GetPendingJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callTimestamps.Add(System.Diagnostics.Stopwatch.GetTimestamp());
                callCount++;
                if (callCount <= 2)
                {
                    return (IReadOnlyList<CaseImportJob>)new List<CaseImportJob>
                    {
                        new() { Id = Guid.NewGuid(), CaseType = "住宅竊盜", OccurrenceDate = 1150329, RawLocation = "臺北市大安區" }
                    };
                }
                throw new OperationCanceledException();
            });

        var config = new ConfigurationBuilder().Build();
        var worker = new CaseImportWorker(jobStore, Substitute.For<ICrimeRepository>(), config, NullLogger<CaseImportWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(2000);
        await worker.StopAsync(CancellationToken.None);

        callCount.Should().BeGreaterThanOrEqualTo(2);
        if (callTimestamps.Count >= 2)
        {
            var elapsedMs = (callTimestamps[1] - callTimestamps[0]) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            elapsedMs.Should().BeLessThan(2000, "should not wait 3s between batches when jobs exist");
        }
    }
}
