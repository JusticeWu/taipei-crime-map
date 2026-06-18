using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TaipeiCrimeMap.Application.Options;
using TaipeiCrimeMap.Infrastructure.Metrics;

namespace TaipeiCrimeMap.API.WebSockets;

public sealed class ServerMetricsWebSocketHandler
{
    private static readonly TimeSpan AuthTimeout = TimeSpan.FromSeconds(5);

    private readonly ServerMetricsService _metricsService;
    private readonly AdminAuthOptions _authOptions;
    private readonly ILogger<ServerMetricsWebSocketHandler> _logger;

    public ServerMetricsWebSocketHandler(
        ServerMetricsService metricsService,
        IOptions<AdminAuthOptions> authOptions,
        ILogger<ServerMetricsWebSocketHandler> logger)
    {
        _metricsService = metricsService;
        _authOptions = authOptions.Value;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        if (!await AuthenticateViaMessageAsync(ws, context.RequestAborted))
        {
            await ws.CloseAsync(
                WebSocketCloseStatus.PolicyViolation,
                "Authentication failed",
                CancellationToken.None);
            return;
        }

        _metricsService.AddConnection();

        var (channelId, reader) = _metricsService.RegisterClientChannel();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);

        try
        {
            await foreach (var bytes in reader.ReadAllAsync(cts.Token))
            {
                if (ws.State != WebSocketState.Open) break;
                await ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            cts.Cancel();
            _metricsService.RemoveConnection();
            _metricsService.UnregisterClientChannel(channelId);
        }
    }

    private async Task<bool> AuthenticateViaMessageAsync(WebSocket ws, CancellationToken requestAborted)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        cts.CancelAfter(AuthTimeout);

        try
        {
            var buffer = new byte[2048];
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

            if (result.MessageType != WebSocketMessageType.Text)
                return false;

            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("token").GetString();

            return !string.IsNullOrEmpty(token) && ValidateBase64(token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("WebSocket 認證逾時（{Timeout}s）", AuthTimeout.TotalSeconds);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WebSocket 認證訊息解析失敗");
            return false;
        }
    }

    private bool ValidateBase64(string base64)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var idx = decoded.IndexOf(':');
            if (idx < 0) return false;

            var username = decoded[..idx];
            var password = decoded[(idx + 1)..];

            return !string.IsNullOrEmpty(_authOptions.Username)
                && username == _authOptions.Username
                && password == _authOptions.Password;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
