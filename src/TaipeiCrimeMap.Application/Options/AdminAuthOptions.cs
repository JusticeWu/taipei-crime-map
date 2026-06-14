namespace TaipeiCrimeMap.Application.Options;

public sealed class AdminAuthOptions
{
    public const string SectionName = "AdminAuth";
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
