using OnlineBanking.Module.Accounts.Domain;
using OnlineBanking.Shared.Contracts;
using OnlineBanking.Shared.Results;

namespace OnlineBanking.Module.Accounts.Application.Abstractions;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Account?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Account?> GetByIbanAsNoTrackingAsync(string normalizedIban, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Account>> ListByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetAccountIdsByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByIbanAsync(string normalizedIban, CancellationToken cancellationToken = default);
    Task AddAsync(Account account, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<Result<TransferExecutionResult>> ExecuteAtomicTransferAsync(
        TransferExecutionRequest request,
        CancellationToken cancellationToken = default);
}
