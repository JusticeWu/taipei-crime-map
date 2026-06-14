using System.Text;
using Microsoft.Extensions.Options;
using TaipeiCrimeMap.Application.Options;

namespace TaipeiCrimeMap.API.Middleware;

/// <summary>
/// 以 HTTP Basic Authentication 保護特定路由（目前用於 /api/crime/coordinate）。
/// 帳密來自 AdminAuth:Username / AdminAuth:Password（環境變數 AdminAuth__Username / AdminAuth__Password）。
/// </summary>
public sealed class BasicAuthMiddleware
{
    private const string Realm = "TaipeiCrimeMap Admin";

    private readonly RequestDelegate _next;
    private readonly AdminAuthOptions _options;

    public BasicAuthMiddleware(RequestDelegate next, IOptions<AdminAuthOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();

        if (!TryGetCredentials(header, out var username, out var password)
            || username != _options.Username
            || password != _options.Password
            || string.IsNullOrEmpty(_options.Username))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = $"Basic realm=\"{Realm}\"";
            return;
        }

        await _next(context);
    }

    private static bool TryGetCredentials(string? header, out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;

        if (string.IsNullOrEmpty(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var encoded = header["Basic ".Length..].Trim();
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var separatorIndex = decoded.IndexOf(':');

            if (separatorIndex < 0)
            {
                return false;
            }

            username = decoded[..separatorIndex];
            password = decoded[(separatorIndex + 1)..];
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
