using OnlineBanking.Module.Clients.Domain;

namespace OnlineBanking.Module.Clients.Application.Abstractions;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Client?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailOrTaxIdAsync(string normalizedEmail, string taxId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Client>> ListAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Client client, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
