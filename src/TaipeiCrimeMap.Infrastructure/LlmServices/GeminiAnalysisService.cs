using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaipeiCrimeMap.Domain.Services;

namespace TaipeiCrimeMap.Infrastructure.LlmServices;

public class GeminiOptions
{
    public const string SectionName = "Gemini";
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gemini-2.0-flash";
}

public sealed class GeminiAnalysisService : ILlmAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiAnalysisService> _logger;

    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiAnalysisService(HttpClient httpClient, IOptions<GeminiOptions> options,
        ILogger<GeminiAnalysisService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateAnalysisAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/{_options.Model}:generateContent?key={_options.ApiKey}";

        var requestBody = new GeminiRequest
        {
            Contents = new[]
            {
                new GeminiContent
                {
                    Parts = new[] { new GeminiPart { Text = prompt } }
                }
            }
        };

        var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);

        var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Gemini API 回傳空結果");

        return text.Trim();
    }

    private sealed class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public GeminiContent[] Contents { get; init; } = Array.Empty<GeminiContent>();
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("parts")]
        public GeminiPart[] Parts { get; init; } = Array.Empty<GeminiPart>();
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }

    private sealed class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; init; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; init; }
    }
}
