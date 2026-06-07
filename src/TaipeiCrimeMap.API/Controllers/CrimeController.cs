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

    public CrimeController(
        ImportCsvCommandHandler importHandler,
        GetCrimesByFilterQueryHandler queryHandler,
        GetHeatmapQueryHandler heatmapHandler)
    {
        _importHandler = importHandler;
        _queryHandler = queryHandler;
        _heatmapHandler = heatmapHandler;
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
    /// 匯入 CSV 的請求物件
    /// </summary>
    public record ImportCsvRequest(string FilePath, CaseType CaseType);
}