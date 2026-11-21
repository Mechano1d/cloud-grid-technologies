using Microsoft.EntityFrameworkCore;
using Npgsql;
using OnlineBanking.Module.Clients.Application.Abstractions;
using OnlineBanking.Module.Clients.Domain;

namespace OnlineBanking.Persistence.Repositories;

internal sealed class ClientRepository(OnlineBankingDbContext dbContext) : IClientRepository
{
    private const int MaxClientsPageLimit = 1000;

    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Client?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Clients.AnyAsync(c => c.Id == id && c.Status == ClientStatus.Active, cancellationToken);

    public Task<Client?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Email == normalizedEmail, cancellationToken);

    public Task<bool> ExistsByEmailOrTaxIdAsync(
        string normalizedEmail,
        string taxId,
        CancellationToken cancellationToken = default) =>
        dbContext.Clients.AnyAsync(c => c.Email == normalizedEmail || c.TaxId == taxId, cancellationToken);

    public async Task<IReadOnlyList<Client>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Clients
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(MaxClientsPageLimit)
            .ToListAsync(cancellationToken);

    public Task AddAsync(Client client, CancellationToken cancellationToken = default) =>
        dbContext.Clients.AddAsync(client, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            foreach (var entry in dbContext.ChangeTracker.Entries<Client>()
                         .Where(e => e.State == EntityState.Added)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }
}
