using OnlineBanking.Module.Accounts.Application.Abstractions;
using OnlineBanking.Module.Accounts.Domain;
using OnlineBanking.Shared.Contracts;
using OnlineBanking.Shared.Results;

namespace OnlineBanking.Module.Accounts.Application;

internal sealed class AccountsApiAdapter(IAccountRepository accountRepository) : IAccountsApi
{
    public async Task<AccountSummaryDto?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsNoTrackingAsync(accountId, cancellationToken);
        return account is null ? null : MapToSummary(account);
    }

    public async Task<AccountSummaryDto?> GetByIbanAsync(string iban, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return null;
        }

        var normalized = iban.Trim().ToUpperInvariant();
        var account = await accountRepository.GetByIbanAsNoTrackingAsync(normalized, cancellationToken);
        return account is null ? null : MapToSummary(account);
    }

    public Task<IReadOnlyList<Guid>> GetAccountIdsByClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default) =>
        accountRepository.GetAccountIdsByClientIdAsync(clientId, cancellationToken);

    public Task<Result<TransferExecutionResult>> ExecuteTransferAsync(
        TransferExecutionRequest request,
        CancellationToken cancellationToken = default) =>
        accountRepository.ExecuteAtomicTransferAsync(request, cancellationToken);

    private static AccountSummaryDto MapToSummary(Account account) =>
        new(
            account.Id,
            account.ClientId,
            account.Iban,
            account.Currency.ToString(),
            account.Balance,
            account.BlockedAmount,
            account.AvailableBalance,
            account.Status.ToString());
}
