using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.Domain.Results;

public record CsvParseResult(IReadOnlyList<TheftCase> Cases, int SkippedCount);