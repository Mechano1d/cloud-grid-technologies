using OnlineBanking.Module.Transactions.Domain;

namespace OnlineBanking.Module.Transactions.Application.Abstractions;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdempotencyKeyAsync(
        Guid initiatedByClientId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListPageAsync(
        IReadOnlyCollection<Guid>? accountIds,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);
}
