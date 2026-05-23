using Microsoft.Extensions.Logging;
using TaipeiCrimeMap.Application.Commands;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Interfaces;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;

namespace TaipeiCrimeMap.Application.Handlers;

public class ImportCsvCommandHandler
{
    private readonly ICsvParser _csvParser;
    private readonly ICrimeRepository _crimeRepository;
    private readonly ILogger<ImportCsvCommandHandler> _logger;

    public ImportCsvCommandHandler(ICsvParser csvParser, ICrimeRepository crimeRepository, ILogger<ImportCsvCommandHandler> logger)
    {
        _csvParser = csvParser;
        _crimeRepository = crimeRepository;
        _logger = logger;
    }

    public async Task<ImportCsvResult> HandleAsync(ImportCsvCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始匯入 CSV：{FilePath}，案類：{CaseType}", command.FilePath, command.CaseType);

        var result = _csvParser.Parse(command.FilePath, command.CaseType);

        await _crimeRepository.AddRangeAsync(result.Cases, cancellationToken);

        _logger.LogInformation("完成匯入 CSV：{FilePath}，成功 {SuccessCount} 筆，跳過 {SkippedCount} 筆", 
            command.FilePath, result.Cases.Count, result.SkippedCount);

        return new ImportCsvResult
        {
            SuccessCount = result.Cases.Count,
            SkippedCount = result.SkippedCount,
            FilePath = command.FilePath,
            CaseType = command.CaseType.ToChineseName()
        };
    }
}
