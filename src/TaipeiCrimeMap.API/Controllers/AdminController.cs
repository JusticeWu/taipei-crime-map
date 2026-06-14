using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace TaipeiCrimeMap.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IMemoryCache _memoryCache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IMemoryCache memoryCache, IConnectionMultiplexer redis, ILogger<AdminController> logger)
    {
        _memoryCache = memoryCache;
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// 清除所有快取：L1（IMemoryCache）+ L2（Garnet/Redis，FLUSHALL）（管理用途，需 Basic Authentication）
    /// </summary>
    [HttpPost("cache/clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ClearCache(CancellationToken cancellationToken)
    {
        var l1Cleared = false;
        if (_memoryCache is MemoryCache memoryCache)
        {
            memoryCache.Clear();
            l1Cleared = true;
        }

        var l2Cleared = false;
        try
        {
            var database = _redis.GetDatabase();
            await database.ExecuteAsync("FLUSHALL");
            l2Cleared = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清除 L2（Garnet/Redis）快取失敗");
        }

        return Ok(new { l1Cleared, l2Cleared });
    }
}
