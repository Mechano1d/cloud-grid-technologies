using Microsoft.AspNetCore.Identity;

namespace OnlineBanking.Infrastructure.Auth;

internal sealed class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object SharedUserMarker = new();

    public string Hash(string password) => _hasher.HashPassword(SharedUserMarker, password);

    public bool Verify(string password, string passwordHash)
    {
        var verification = _hasher.VerifyHashedPassword(SharedUserMarker, passwordHash, password);
        return verification is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
