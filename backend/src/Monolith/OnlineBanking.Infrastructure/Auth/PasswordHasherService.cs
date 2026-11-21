using Microsoft.AspNetCore.Identity;

namespace OnlineBanking.Infrastructure.Auth;

internal sealed class PasswordHasherService : IPasswordHasherService
{
    private static readonly object SharedUserMarker = new();
    private readonly PasswordHasher<object> _hasher = new();

    public PasswordHasherService()
    {
        DummyHash = _hasher.HashPassword(SharedUserMarker, "dummy-timing-guard-password");
    }

    public string DummyHash { get; }

    public string Hash(string password) => _hasher.HashPassword(SharedUserMarker, password);

    public bool Verify(string password, string passwordHash)
    {
        var verification = _hasher.VerifyHashedPassword(SharedUserMarker, passwordHash, password);
        return verification is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
