namespace TaipeiCrimeMap.Domain.Services;

public interface ILlmAnalysisService
{
    Task<string> GenerateAnalysisAsync(string prompt, CancellationToken cancellationToken = default);
}
