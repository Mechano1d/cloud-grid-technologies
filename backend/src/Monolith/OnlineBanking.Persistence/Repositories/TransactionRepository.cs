using Microsoft.EntityFrameworkCore;
using Npgsql;
using OnlineBanking.Module.Transactions.Application.Abstractions;
using OnlineBanking.Module.Transactions.Domain;

namespace OnlineBanking.Persistence.Repositories;

internal sealed class TransactionRepository(OnlineBankingDbContext dbContext) : ITransactionRepository
{
    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<Transaction?> GetByIdempotencyKeyAsync(
        Guid initiatedByClientId,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        dbContext.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.InitiatedByClientId == initiatedByClientId && t.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public async Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListPageAsync(
        IReadOnlyCollection<Guid>? accountIds,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Transactions.AsNoTracking().AsQueryable();

        if (accountIds is not null)
        {
            if (accountIds.Count == 0)
            {
                return ([], 0);
            }

            query = query.Where(t => accountIds.Contains(t.FromAccountId) || accountIds.Contains(t.ToAccountId));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = checked((int)((long)(page - 1) * pageSize));
        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .ThenByDescending(t => t.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
        dbContext.Transactions.AddAsync(transaction, cancellationToken).AsTask();

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
            foreach (var entry in dbContext.ChangeTracker.Entries<Transaction>()
                         .Where(e => e.State == EntityState.Added)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }
}
