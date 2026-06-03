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

    public CrimeController(
        ImportCsvCommandHandler importHandler,
        GetCrimesByFilterQueryHandler queryHandler
    )
    {
        _importHandler = importHandler;
        _queryHandler = queryHandler;
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
    /// 依條件查詢案件
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TheftCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCrimes(
        [FromQuery] CaseType? caseType,
        [FromQuery] string? districtName,
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        [FromQuery] string? rawTimeSlot,
        CancellationToken cancellationToken)
    {
        var query = new GetCrimesByFilterQuery(
            CaseType: caseType,
            DistrictName: districtName,
            YearFrom: yearFrom,
            YearTo: yearTo,
            RawTimeSlot: rawTimeSlot);

        var results = await _queryHandler.HandleAsync(query, cancellationToken);
        return Ok(results);
    }


    /// <summary>
    /// 匯入 CSV 的請求物件
    /// </summary>
    public record ImportCsvRequest(string FilePath, CaseType CaseType);
}