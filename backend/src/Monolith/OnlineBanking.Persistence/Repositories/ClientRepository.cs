using Microsoft.EntityFrameworkCore;
using OnlineBanking.Module.Clients.Application.Abstractions;
using OnlineBanking.Module.Clients.Domain;

namespace OnlineBanking.Persistence.Repositories;

internal sealed class ClientRepository(OnlineBankingDbContext dbContext) : IClientRepository
{
    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Client?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        dbContext.Clients.FirstOrDefaultAsync(c => c.Email == normalizedEmail, cancellationToken);

    public Task<bool> ExistsByEmailOrTaxIdAsync(
        string normalizedEmail,
        string taxId,
        CancellationToken cancellationToken = default) =>
        dbContext.Clients.AnyAsync(c => c.Email == normalizedEmail || c.TaxId == taxId, cancellationToken);

    public async Task<IReadOnlyList<Client>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Clients
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task AddAsync(Client client, CancellationToken cancellationToken = default) =>
        dbContext.Clients.AddAsync(client, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
