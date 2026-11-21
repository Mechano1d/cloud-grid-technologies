namespace OnlineBanking.Shared.Contracts;

public interface IClientsApi
{
    Task<bool> IsActiveClientAsync(Guid clientId, CancellationToken cancellationToken = default);
}
