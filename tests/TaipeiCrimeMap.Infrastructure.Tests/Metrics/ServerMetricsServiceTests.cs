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

        metrics.CpuPercent.Should().BeInRange(0, 100 * System.Environment.ProcessorCount);
    }

    [Fact]
    public void GetMetrics_StaticHardwareInfo_IsPopulated()
    {
        var service = new ServerMetricsService();

        var metrics = service.GetMetrics(0);

        metrics.CpuCores.Should().BeGreaterThan(0);
        metrics.TotalMemoryMb.Should().BeGreaterThan(0);
        metrics.OsDescription.Should().NotBeNullOrWhiteSpace();
        metrics.DotNetVersion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetMetrics_StaticHardwareInfo_DoesNotChangeBetweenCalls()
    {
        var service = new ServerMetricsService();

        var first = service.GetMetrics(0);
        var second = service.GetMetrics(0);

        second.CpuCores.Should().Be(first.CpuCores);
        second.TotalMemoryMb.Should().Be(first.TotalMemoryMb);
        second.OsDescription.Should().Be(first.OsDescription);
        second.DotNetVersion.Should().Be(first.DotNetVersion);
    }

    [Fact]
    public void GetMetrics_HostId_IsPopulated()
    {
        var service = new ServerMetricsService();

        var metrics = service.GetMetrics(0);

        metrics.HostId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetMetrics_Environment_IsPopulated()
    {
        var service = new ServerMetricsService();

        var metrics = service.GetMetrics(0);

        metrics.Environment.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetMetrics_HostId_IsConsistentAcrossCalls()
    {
        var service = new ServerMetricsService();

        var first = service.GetMetrics(0);
        var second = service.GetMetrics(0);

        second.HostId.Should().Be(first.HostId);
        second.Environment.Should().Be(first.Environment);
    }

    [Fact]
    public async Task PublishAsync_WithoutRedis_DoesNotThrow()
    {
        // 建立無 Redis 的 service（null），PublishAsync 應為 no-op
        var service = new ServerMetricsService(redis: null);
        var metrics = service.GetMetrics(0);

        var act = async () => await service.PublishAsync(metrics);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void UnregisterClientChannel_AfterRegister_CompletesReader()
    {
        var service = new ServerMetricsService();

        var (id, reader) = service.RegisterClientChannel();
        service.UnregisterClientChannel(id);

        reader.Completion.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_CalledTwice_DoesNotThrow()
    {
        var service = new ServerMetricsService();
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        var act = async () => await service.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
