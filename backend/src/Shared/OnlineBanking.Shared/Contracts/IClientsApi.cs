namespace OnlineBanking.Shared.Contracts;

public sealed record ClientSummaryDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    string Status);

public interface IClientsApi
{
    Task<ClientSummaryDto?> GetByIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveClientAsync(Guid clientId, CancellationToken cancellationToken = default);
}
