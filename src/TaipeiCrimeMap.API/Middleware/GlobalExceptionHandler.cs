using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TaipeiCrimeMap.Domain.Exceptions;

namespace TaipeiCrimeMap.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }
    
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "請求參數不正確"),
            FileNotFoundException => (StatusCodes.Status404NotFound, "找不到指定的檔案"),
            _ => (StatusCodes.Status500InternalServerError, "伺服器發生錯誤")
        };

        _logger.LogError(exception, "例外發生：{Message}", exception.Message);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        
        return true;
    }
}