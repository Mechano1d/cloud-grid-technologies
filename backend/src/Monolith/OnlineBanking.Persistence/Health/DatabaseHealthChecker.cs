namespace OnlineBanking.Persistence.Health;

internal sealed class DatabaseHealthChecker(OnlineBankingDbContext dbContext) : IDatabaseHealthChecker
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.CanConnectAsync(cancellationToken);
}
