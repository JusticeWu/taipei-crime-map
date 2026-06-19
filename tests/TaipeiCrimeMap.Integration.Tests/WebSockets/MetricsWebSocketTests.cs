using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TaipeiCrimeMap.Infrastructure.Metrics;

namespace TaipeiCrimeMap.Integration.Tests.WebSockets;

public class MetricsWebSocketTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MetricsWebSocketTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<WebSocket> ConnectAndAuthAsync(CancellationToken ct)
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        var ws = await wsClient.ConnectAsync(new Uri("ws://localhost/ws/metrics"), ct);

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{CustomWebApplicationFactory.AdminUsername}:{CustomWebApplicationFactory.AdminPassword}"));
        var msg = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { token }));
        await ws.SendAsync(new ArraySegment<byte>(msg), WebSocketMessageType.Text, true, ct);

        await Task.Delay(200, ct);
        return ws;
    }

    private void TriggerBroadcast()
    {
        var svc = _factory.Services.GetRequiredService<ServerMetricsService>();
        var metrics = svc.GetMetrics(0);
        svc.BroadcastToClients(JsonSerializer.Serialize(metrics,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    [Fact(Timeout = 15000)]
    public async Task WsMetrics_WithWrongToken_ClosesWithPolicyViolation()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var wsClient = _factory.Server.CreateWebSocketClient();
        using var ws = await wsClient.ConnectAsync(new Uri("ws://localhost/ws/metrics"), cts.Token);

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes("wrong:credentials"));
        var msg = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { token }));
        await ws.SendAsync(new ArraySegment<byte>(msg), WebSocketMessageType.Text, true, cts.Token);

        var buffer = new byte[1024];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
        result.MessageType.Should().Be(WebSocketMessageType.Close);
        ws.CloseStatus.Should().Be(WebSocketCloseStatus.PolicyViolation);
    }

    [Fact(Timeout = 15000)]
    public async Task WsMetrics_WithNoToken_ClosesOnTimeout()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var wsClient = _factory.Server.CreateWebSocketClient();
        using var ws = await wsClient.ConnectAsync(new Uri("ws://localhost/ws/metrics"), cts.Token);

        var buffer = new byte[1024];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
        result.MessageType.Should().Be(WebSocketMessageType.Close);
        ws.CloseStatus.Should().Be(WebSocketCloseStatus.PolicyViolation);
    }

    [Fact(Timeout = 15000)]
    public async Task WsMetrics_WithCorrectToken_StaysOpen()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var ws = await ConnectAndAuthAsync(cts.Token);

        TriggerBroadcast();

        var buffer = new byte[8192];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
        result.MessageType.Should().Be(WebSocketMessageType.Text);

        ws.State.Should().Be(WebSocketState.Open);
        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);
    }

    [Fact(Timeout = 15000)]
    public async Task WsMetrics_AfterAuth_ReceivesJsonWithRequiredFields()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var ws = await ConnectAndAuthAsync(cts.Token);

        TriggerBroadcast();

        var buffer = new byte[8192];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

        result.MessageType.Should().Be(WebSocketMessageType.Text);
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("hostId", out _).Should().BeTrue("hostId 欄位應存在");
        root.TryGetProperty("cpuPercent", out _).Should().BeTrue("cpuPercent 欄位應存在");
        root.TryGetProperty("memoryMb", out _).Should().BeTrue("memoryMb 欄位應存在");

        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);
    }
}
