using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OnlineBanking.Shared.Time;

namespace OnlineBanking.Infrastructure.Auth;

internal sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public JwtTokenGenerator(IOptions<JwtOptions> options, IDateTimeProvider dateTimeProvider)
    {
        _options = options.Value;
        _dateTimeProvider = dateTimeProvider;

        var keyBytes = Encoding.UTF8.GetBytes(_options.SecretKey ?? string.Empty);
        if (string.IsNullOrWhiteSpace(_options.SecretKey) || keyBytes.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 UTF-8 bytes long.");
        }

        var key = new SymmetricSecurityKey(keyBytes);
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public JwtTokenResult Generate(Guid clientId, string email, string role)
    {
        var now = _dateTimeProvider.UtcNow.UtcDateTime;
        var expiresAt = now.AddMinutes(_options.ExpirationMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = _signingCredentials,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, clientId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtClaimNames.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ])
        };

        var token = _tokenHandler.CreateToken(descriptor);
        return new JwtTokenResult(token, _options.ExpirationMinutes * 60);
    }
}
