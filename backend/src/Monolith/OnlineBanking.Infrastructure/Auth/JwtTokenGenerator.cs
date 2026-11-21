using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OnlineBanking.Shared.Time;

namespace OnlineBanking.Infrastructure.Auth;

internal sealed class JwtTokenGenerator(
    IOptions<JwtOptions> options,
    IDateTimeProvider dateTimeProvider) : IJwtTokenGenerator
{
    private readonly JwtOptions _options = options.Value;

    public JwtTokenResult Generate(Guid clientId, string email, string role)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey) || _options.SecretKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 characters long.");
        }

        var now = dateTimeProvider.UtcNow.UtcDateTime;
        var expiresAt = now.AddMinutes(_options.ExpirationMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = credentials,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, clientId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim("role", role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ])
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(descriptor);

        return new JwtTokenResult(token, _options.ExpirationMinutes * 60);
    }
}
