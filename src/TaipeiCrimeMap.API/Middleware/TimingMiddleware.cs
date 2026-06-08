using System.Diagnostics;
using Microsoft.Extensions.Options;
using TaipeiCrimeMap.Application.Options;

namespace TaipeiCrimeMap.API.Middleware;

/// <summary>
/// 記錄每個 HTTP 請求的總耗時。
/// 只在 Timing:Enabled = true 時輸出 log。
/// </summary>
public sealed class TimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TimingMiddleware> _logger;
    private readonly bool _enabled;

    public TimingMiddleware(
        RequestDelegate next,
        ILogger<TimingMiddleware> logger,
        IOptions<TimingOptions> options)
    {
        _next    = next;
        _logger  = logger;
        _enabled = options.Value.Enabled;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_enabled)
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            _logger.LogInformation(
                "[Timing] {Method} {Path} → {StatusCode} | 總耗時={ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds);
        }
    }
}
