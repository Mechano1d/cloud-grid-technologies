using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OnlineBanking.Infrastructure.Auth;
using OnlineBanking.Shared.Time;

namespace OnlineBanking.Infrastructure;

public static class InfrastructureInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);

        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || jwtOptions.SecretKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SecretKey must be configured and at least 32 characters long.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization();

        return services;
    }
}
