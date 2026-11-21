using OnlineBanking.Shared.Results;

namespace OnlineBanking.Shared.Contracts;

public sealed record AccountSummaryDto(
    Guid Id,
    Guid ClientId,
    string Iban,
    string Currency,
    decimal Balance,
    decimal BlockedAmount,
    decimal AvailableBalance,
    string Status);

public sealed record TransferExecutionRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string ExpectedCurrency);

public sealed record TransferExecutionResult(
    Guid FromAccountId,
    Guid ToAccountId,
    string Currency,
    decimal Amount);

public interface IAccountsApi
{
    Task<AccountSummaryDto?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<AccountSummaryDto?> GetByIbanAsync(string iban, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetAccountIdsByClientAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<Result<TransferExecutionResult>> ExecuteTransferAsync(
        TransferExecutionRequest request,
        CancellationToken cancellationToken = default);
}
