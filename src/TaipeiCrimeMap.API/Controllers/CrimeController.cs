using Microsoft.AspNetCore.Mvc;
using TaipeiCrimeMap.Application.Commands;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CrimeController : ControllerBase
{
    private readonly ImportCsvCommandHandler _importHandler;

    public CrimeController(
        ImportCsvCommandHandler importHandler
    )
    {
        _importHandler = importHandler;
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
    /// 匯入 CSV 的請求物件
    /// </summary>
    public record ImportCsvRequest(string FilePath, CaseType CaseType);
}