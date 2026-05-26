using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.Application.Commands;

public record ImportCsvCommand(string FilePath, CaseType CaseType);
