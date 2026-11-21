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
        Principal?.FindFirstValue(JwtClaimNames.Role) ?? Principal?.FindFirstValue(ClaimTypes.Role);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && ClientId.HasValue;

    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);

    public bool TryGetClientId(out Guid clientId)
    {
        if (ClientId is { } id)
        {
            clientId = id;
            return true;
        }

        clientId = Guid.Empty;
        return false;
    }

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
}
