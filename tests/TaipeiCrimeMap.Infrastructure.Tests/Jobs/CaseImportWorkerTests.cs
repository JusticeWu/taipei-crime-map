using FluentAssertions;
using StackExchange.Redis;
using TaipeiCrimeMap.Domain.Exceptions;
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
}
