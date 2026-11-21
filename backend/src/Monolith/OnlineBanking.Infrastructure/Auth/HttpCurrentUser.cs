using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace OnlineBanking.Infrastructure.Auth;

internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? ClientId
    {
        get
        {
            var raw = Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Role =>
        Principal?.FindFirstValue("role") ?? Principal?.FindFirstValue(ClaimTypes.Role);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && ClientId.HasValue;

    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
}
