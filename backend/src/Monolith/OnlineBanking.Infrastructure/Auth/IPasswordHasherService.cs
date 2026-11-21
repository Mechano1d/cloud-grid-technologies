namespace OnlineBanking.Infrastructure.Auth;

public interface IPasswordHasherService
{
    string DummyHash { get; }
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}
