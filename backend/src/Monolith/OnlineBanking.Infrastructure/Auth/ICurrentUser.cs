namespace OnlineBanking.Infrastructure.Auth;

public interface ICurrentUser
{
    Guid? ClientId { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    bool TryGetClientId(out Guid clientId);
}
