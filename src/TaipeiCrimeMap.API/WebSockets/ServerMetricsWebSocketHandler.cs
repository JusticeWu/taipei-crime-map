using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Options;
using TaipeiCrimeMap.Application.Options;
using TaipeiCrimeMap.Infrastructure.Metrics;

namespace TaipeiCrimeMap.API.WebSockets;

public sealed class ServerMetricsWebSocketHandler
{
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

        if (!IsAuthenticated(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
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

    private bool IsAuthenticated(HttpContext context)
    {
        var token = context.Request.Query["token"].ToString();
        if (!string.IsNullOrEmpty(token))
            return ValidateBase64(token);

        var header = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(header)
            && header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return ValidateBase64(header["Basic ".Length..].Trim());

        return false;
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
