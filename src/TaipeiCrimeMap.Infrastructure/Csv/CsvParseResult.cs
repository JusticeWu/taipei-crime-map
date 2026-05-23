using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.Infrastructure.Csv;

public record CsvParseResult(IReadOnlyList<TheftCase> Cases, int SkippedCount);