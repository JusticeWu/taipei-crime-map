using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Results;

namespace TaipeiCrimeMap.Domain.Services;

public interface ICsvParser
{
    CsvParseResult Parse(string filePath, CaseType caseType);
}