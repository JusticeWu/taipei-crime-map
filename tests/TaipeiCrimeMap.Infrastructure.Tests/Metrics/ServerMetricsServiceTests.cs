using FluentAssertions;
using TaipeiCrimeMap.Infrastructure.Metrics;

namespace TaipeiCrimeMap.Infrastructure.Tests.Metrics;

public class ServerMetricsServiceTests
{
    [Fact]
    public void GetMetrics_ReturnsNonEmptyFields()
    {
        var service = new ServerMetricsService();

        var metrics = service.GetMetrics(connectionCount: 0);

        metrics.Should().NotBeNull();
        metrics.MemoryMb.Should().BeGreaterThan(0);
        metrics.GcMemoryMb.Should().BeGreaterThan(0);
        metrics.ThreadCount.Should().BeGreaterThan(0);
        metrics.Uptime.Should().NotBeNullOrWhiteSpace();
        metrics.CpuPercent.Should().BeGreaterThanOrEqualTo(0);
        metrics.ConnectionCount.Should().Be(0);
    }

    [Fact]
    public void GetMetrics_ReflectsGivenConnectionCount()
    {
        var service = new ServerMetricsService();

        var metrics = service.GetMetrics(connectionCount: 3);

        metrics.ConnectionCount.Should().Be(3);
    }

    [Fact]
    public void GetMetrics_CalledTwice_CpuPercentInValidRange()
    {
        var service = new ServerMetricsService();

        service.GetMetrics(0);
        System.Threading.Thread.Sleep(50);
        var metrics = service.GetMetrics(0);

        metrics.CpuPercent.Should().BeInRange(0, 100 * Environment.ProcessorCount);
    }
}
