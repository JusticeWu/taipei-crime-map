using System.Diagnostics;

namespace TaipeiCrimeMap.API.Middleware;

/// <summary>
/// 記錄每個 HTTP 請求的結構化觀測資料（來源 IP、方法、路徑、狀態碼、耗時、TraceId）。
/// 獨立於 TimingMiddleware，不影響既有的耗時統計邏輯。
/// </summary>
public sealed class ObservabilityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ObservabilityMiddleware> _logger;

    public ObservabilityMiddleware(RequestDelegate next, ILogger<ObservabilityMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            var clientIp   = GetClientIp(context);
            var method     = context.Request.Method;
            var path       = context.Request.Path.Value;
            var statusCode = context.Response.StatusCode;
            var elapsedMs  = sw.ElapsedMilliseconds;
            var traceId    = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

            var logLevel = statusCode switch
            {
                >= 500 => LogLevel.Error,
                >= 400 => LogLevel.Warning,
                _      => LogLevel.Information,
            };

            _logger.Log(
                logLevel,
                "[Observability] {ClientIp} {Method} {Path} → {StatusCode} | 耗時={ElapsedMs}ms | TraceId={TraceId}",
                clientIp,
                method,
                path,
                statusCode,
                elapsedMs,
                traceId);
        }
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // X-Forwarded-For 可能包含多個以逗號分隔的 IP，第一個為原始用戶端
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
