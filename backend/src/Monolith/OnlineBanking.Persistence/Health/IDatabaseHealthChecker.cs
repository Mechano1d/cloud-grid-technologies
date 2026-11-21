namespace OnlineBanking.Persistence.Health;

public interface IDatabaseHealthChecker
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
