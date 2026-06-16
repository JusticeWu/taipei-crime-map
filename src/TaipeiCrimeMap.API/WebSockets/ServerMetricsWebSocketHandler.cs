using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TaipeiCrimeMap.Application.Options;
using TaipeiCrimeMap.Infrastructure.Metrics;

namespace TaipeiCrimeMap.API.WebSockets;

public sealed class ServerMetricsWebSocketHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ServerMetricsService _metrics;
    private readonly AdminAuthOptions _authOptions;
    private volatile int _connectionCount;

    public ServerMetricsWebSocketHandler(
        ServerMetricsService metrics,
        IOptions<AdminAuthOptions> authOptions)
    {
        _metrics = metrics;
        _authOptions = authOptions.Value;
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
        Interlocked.Increment(ref _connectionCount);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);

            while (ws.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
            {
                var snapshot = _metrics.GetMetrics(_connectionCount);
                var json = JsonSerializer.Serialize(snapshot, JsonOptions);
                var bytes = Encoding.UTF8.GetBytes(json);

                await ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cts.Token);

                await Task.Delay(1000, cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            Interlocked.Decrement(ref _connectionCount);
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
