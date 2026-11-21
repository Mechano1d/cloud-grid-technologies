namespace OnlineBanking.Infrastructure.Auth;

public sealed record JwtTokenResult(string AccessToken, int ExpiresInSeconds);

public interface IJwtTokenGenerator
{
    JwtTokenResult Generate(Guid clientId, string email, string role);
}
