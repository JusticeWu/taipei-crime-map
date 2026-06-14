using Microsoft.AspNetCore.Mvc;
using TaipeiCrimeMap.Application.Commands;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CrimeController : ControllerBase
{
    private readonly ImportCsvCommandHandler _importHandler;
    private readonly GetCrimesByFilterQueryHandler _queryHandler;
    private readonly GetHeatmapQueryHandler _heatmapHandler;
    private readonly GeocodeBatchCommandHandler _geocodeHandler;
    private readonly GetCrimeStatsQueryHandler _statsHandler;
    private readonly GetCrimeByIdQueryHandler _byIdHandler;
    private readonly UpdateCoordinateByLocationCommandHandler _updateCoordinateHandler;

    public CrimeController(
        ImportCsvCommandHandler importHandler,
        GetCrimesByFilterQueryHandler queryHandler,
        GetHeatmapQueryHandler heatmapHandler,
        GeocodeBatchCommandHandler geocodeHandler,
        GetCrimeStatsQueryHandler statsHandler,
        GetCrimeByIdQueryHandler byIdHandler,
        UpdateCoordinateByLocationCommandHandler updateCoordinateHandler)
    {
        _importHandler = importHandler;
        _queryHandler = queryHandler;
        _heatmapHandler = heatmapHandler;
        _geocodeHandler = geocodeHandler;
        _statsHandler = statsHandler;
        _byIdHandler = byIdHandler;
        _updateCoordinateHandler = updateCoordinateHandler;
    }

    /// <summary>
    /// 匯入 CSV 案件資料
    /// </summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportCsvResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportCsv([FromBody] ImportCsvRequest request, CancellationToken cancellationToken)
    {
        var command = new ImportCsvCommand(request.FilePath, request.CaseType);
        var result = await _importHandler.HandleAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 依條件查詢案件（支援分頁）
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TheftCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCrimes(
        [FromQuery] CaseType? caseType,
        [FromQuery] string? districtName,
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        [FromQuery] string? rawTimeSlot,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCrimesByFilterQuery(
            CaseType: caseType,
            DistrictName: districtName,
            YearFrom: yearFrom,
            YearTo: yearTo,
            RawTimeSlot: rawTimeSlot,
            Page: page,
            PageSize: pageSize);

        var results = await _queryHandler.HandleAsync(query, cancellationToken);
        return Ok(results);
    }


    /// <summary>
    /// 點位圖專用端點：只回傳 lat/lng/caseType/occurredDate，
    /// 省略不需要的欄位，大幅降低 JSON 大小（約減少 70%）
    /// </summary>
    [HttpGet("points")]
    [ProducesResponseType(typeof(PagedResult<PointCrimeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCrimePoints(
        [FromQuery] CaseType? caseType,
        [FromQuery] string? districtName,
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 500,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCrimesByFilterQuery(
            CaseType: caseType,
            DistrictName: districtName,
            YearFrom: yearFrom,
            YearTo: yearTo,
            Page: page,
            PageSize: pageSize);

        var full = await _queryHandler.HandleAsync(query, cancellationToken);
        var points = full.Data
            .Select(d => new PointCrimeDto(d.Id, d.Latitude, d.Longitude, d.CaseType, d.OccurredDate))
            .ToList();
        return Ok(new PagedResult<PointCrimeDto>(points, full.Total, full.Page, full.PageSize, full.TotalPages));
    }

    /// <summary>
    /// 點位圖 popup 點擊後查詢單筆案件詳細資料（行政區、時段、地點）
    /// </summary>
    [HttpGet("points/{id:guid}")]
    [ProducesResponseType(typeof(CrimeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCrimePointDetail(Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await _byIdHandler.HandleAsync(new GetCrimeByIdQuery(id), cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// 統計圖表資料：行政區分布、時段分布彙總計數
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(CrimeStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCrimeStats(
        [FromQuery] CaseType? caseType,
        [FromQuery] string? districtName,
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCrimeStatsQuery(caseType, districtName, yearFrom, yearTo);
        var result = await _statsHandler.HandleAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 行政區案件密度，供熱力圖使用（每區一點，瞬間渲染）
    /// </summary>
    [HttpGet("heatmap")]
    [ProducesResponseType(typeof(IReadOnlyList<HeatmapPointDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHeatmap(
        [FromQuery] CaseType? caseType,
        [FromQuery] string? districtName,
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        CancellationToken cancellationToken = default)
    {
        var query = new GetHeatmapQuery(caseType, districtName, yearFrom, yearTo);
        var result = await _heatmapHandler.HandleAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 批次補齊座標：對 Latitude/Longitude 為 NULL 的案件執行 Geocoding，
    /// 優先複用相同 RawLocation 的既有座標，剩餘的才呼叫 Google Maps API
    /// </summary>
    [HttpPost("geocode")]
    [ProducesResponseType(typeof(GeocodeBatchResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GeocodeBatch(
        [FromQuery] int batchSize = 10,
        CancellationToken cancellationToken = default)
    {
        var command = new GeocodeBatchCommand(batchSize);
        var result = await _geocodeHandler.HandleAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 驗證管理頁面的 Basic Authentication 是否正確（受 BasicAuthMiddleware 保護）
    /// </summary>
    [HttpGet("coordinate/ping")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult CoordinatePing() => Ok();

    /// <summary>
    /// 依 RawLocation 更新座標（管理用途，需 Basic Authentication）
    /// </summary>
    [HttpPatch("coordinate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCoordinate(
        [FromBody] UpdateCoordinateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RawLocation))
        {
            return BadRequest("rawLocation 為必填欄位。");
        }

        if (request.Latitude is < -90 or > 90)
        {
            return BadRequest("latitude 必須在 -90 到 90 之間。");
        }

        if (request.Longitude is < -180 or > 180)
        {
            return BadRequest("longitude 必須在 -180 到 180 之間。");
        }

        var command = new UpdateCoordinateByLocationCommand(request.RawLocation, request.Latitude, request.Longitude);
        var affected = await _updateCoordinateHandler.HandleAsync(command, cancellationToken);

        return affected == 0 ? NotFound() : Ok(new { affected });
    }

    /// <summary>
    /// 匯入 CSV 的請求物件
    /// </summary>
    public record ImportCsvRequest(string FilePath, CaseType CaseType);

    /// <summary>
    /// 更新座標的請求物件
    /// </summary>
    public record UpdateCoordinateRequest(string RawLocation, double Latitude, double Longitude);
}