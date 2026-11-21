namespace OnlineBanking.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "OnlineBanking.Api";
    public string Audience { get; init; } = "OnlineBanking.Web";
    public string SecretKey { get; init; } = string.Empty;
    public int ExpirationMinutes { get; init; } = 60;
}
