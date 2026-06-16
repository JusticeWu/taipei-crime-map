using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace TaipeiCrimeMap.Integration.Tests.WebSockets;

public class MetricsWebSocketTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MetricsWebSocketTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetWsMetrics_WithoutAuthorization_Returns401()
    {
        var response = await _client.GetAsync("/ws/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWsMetrics_WithWrongCredentials_Returns401()
    {
        using var client = _factory.CreateClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("wrong:credentials"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await client.GetAsync("/ws/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWsMetrics_WithCorrectCredentials_Returns101()
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{CustomWebApplicationFactory.AdminUsername}:{CustomWebApplicationFactory.AdminPassword}"));
        wsClient.ConfigureRequest = req =>
            req.Headers["Authorization"] = $"Basic {credentials}";

        using var ws = await wsClient.ConnectAsync(
            new Uri("ws://localhost/ws/metrics"), CancellationToken.None);

        ws.State.Should().Be(WebSocketState.Open);
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }

    [Fact]
    public async Task GetWsMetrics_AfterConnect_ReceivesJsonWithRequiredFields()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var wsClient = _factory.Server.CreateWebSocketClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{CustomWebApplicationFactory.AdminUsername}:{CustomWebApplicationFactory.AdminPassword}"));
        wsClient.ConfigureRequest = req =>
            req.Headers["Authorization"] = $"Basic {credentials}";

        using var ws = await wsClient.ConnectAsync(new Uri("ws://localhost/ws/metrics"), cts.Token);

        var buffer = new byte[8192];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

        result.MessageType.Should().Be(WebSocketMessageType.Text);
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("hostId", out _).Should().BeTrue("hostId 欄位應存在");
        root.TryGetProperty("cpuPercent", out _).Should().BeTrue("cpuPercent 欄位應存在");
        root.TryGetProperty("memoryMb", out _).Should().BeTrue("memoryMb 欄位應存在");

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);
    }
}
