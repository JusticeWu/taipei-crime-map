using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Results;

namespace TaipeiCrimeMap.Application.Interfaces;

public interface ICsvParser
{
    CsvParseResult Parse(string filePath, CaseType caseType);
}
