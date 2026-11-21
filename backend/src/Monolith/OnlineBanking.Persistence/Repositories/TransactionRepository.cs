using Microsoft.EntityFrameworkCore;
using OnlineBanking.Module.Transactions.Application.Abstractions;
using OnlineBanking.Module.Transactions.Domain;

namespace OnlineBanking.Persistence.Repositories;

internal sealed class TransactionRepository(OnlineBankingDbContext dbContext) : ITransactionRepository
{
    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Transactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

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
        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
        dbContext.Transactions.AddAsync(transaction, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
