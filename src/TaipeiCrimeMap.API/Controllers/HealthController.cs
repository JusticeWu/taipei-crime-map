using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;

namespace TaipeiCrimeMap.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("health-api")]
public class HealthController : ControllerBase
{
    private readonly string _connectionString;
    private readonly string _apiKey;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IConfiguration configuration, ILogger<HealthController> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _apiKey = configuration["HealthCheck:ApiKey"] ?? string.Empty;
        _logger = logger;
    }

    [HttpGet("db-ping")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DbPing(CancellationToken cancellationToken)
    {
        var requestKey = Request.Headers["X-API-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(_apiKey) || requestKey != _apiKey)
            return Unauthorized(new { status = "unauthorized" });

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(cancellationToken);

            _logger.LogInformation("db-ping 成功");
            return Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "db-ping 失敗");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { status = "error", message = ex.Message });
        }
    }
}
