using System.Data;
using Microsoft.EntityFrameworkCore;
using OnlineBanking.Module.Accounts.Application.Abstractions;
using OnlineBanking.Module.Accounts.Domain;
using OnlineBanking.Shared.Contracts;
using OnlineBanking.Shared.Results;

namespace OnlineBanking.Persistence.Repositories;

internal sealed class AccountRepository(OnlineBankingDbContext dbContext) : IAccountRepository
{
    public Task<Account?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Account?> GetByIbanAsNoTrackingAsync(string normalizedIban, CancellationToken cancellationToken = default) =>
        dbContext.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Iban == normalizedIban, cancellationToken);

    public async Task<IReadOnlyList<Account>> ListByClientIdAsync(
        Guid clientId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Accounts
            .AsNoTracking()
            .Where(a => a.ClientId == clientId)
            .OrderByDescending(a => a.OpenedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetAccountIdsByClientIdAsync(
        Guid clientId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Accounts
            .AsNoTracking()
            .Where(a => a.ClientId == clientId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByIbanAsync(string normalizedIban, CancellationToken cancellationToken = default) =>
        dbContext.Accounts.AnyAsync(a => a.Iban == normalizedIban, cancellationToken);

    public Task AddAsync(Account account, CancellationToken cancellationToken = default) =>
        dbContext.Accounts.AddAsync(account, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<Result<Account>> CloseAccountAtomicAsync(
        Guid accountId,
        Guid ownerClientId,
        CancellationToken cancellationToken = default)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<Account>().ToList())
        {
            entry.State = EntityState.Detached;
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var account = await dbContext.Accounts
            .FromSqlInterpolated($"SELECT * FROM accounts.accounts WHERE id = {accountId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<Account>(DomainError.NotFound(
                "Accounts.NotFound",
                "Account was not found."));
        }

        if (account.ClientId != ownerClientId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<Account>(DomainError.Forbidden(
                "Accounts.Forbidden",
                "You do not have permission to close this account."));
        }

        var closeResult = account.Close();
        if (closeResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<Account>(closeResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(account);
    }

    public async Task<Result<TransferExecutionResult>> ExecuteAtomicTransferAsync(
        TransferExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<Account>().ToList())
        {
            entry.State = EntityState.Detached;
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var (firstId, secondId) = request.FromAccountId.CompareTo(request.ToAccountId) < 0
            ? (request.FromAccountId, request.ToAccountId)
            : (request.ToAccountId, request.FromAccountId);

        // Deterministic primary-key lock order prevents deadlocks across concurrent opposite transfers.
        var lockedAccounts = await dbContext.Accounts
            .FromSqlInterpolated(
                $"SELECT * FROM accounts.accounts WHERE id IN ({firstId}, {secondId}) ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);

        var fromAccount = lockedAccounts.FirstOrDefault(a => a.Id == request.FromAccountId);
        if (fromAccount is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<TransferExecutionResult>(DomainError.NotFound(
                "Accounts.SourceNotFound",
                "Source account was not found."));
        }

        var toAccount = lockedAccounts.FirstOrDefault(a => a.Id == request.ToAccountId);
        if (toAccount is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<TransferExecutionResult>(DomainError.NotFound(
                "Accounts.DestinationNotFound",
                "Destination account was not found."));
        }

        if (fromAccount.Currency != toAccount.Currency ||
            !string.Equals(fromAccount.Currency.ToString(), request.ExpectedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<TransferExecutionResult>(DomainError.Conflict(
                "Accounts.CurrencyMismatch",
                "Transfers require matching account currencies."));
        }

        var debitResult = fromAccount.Debit(request.Amount);
        if (debitResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<TransferExecutionResult>(debitResult.Error);
        }

        var creditResult = toAccount.Credit(request.Amount);
        if (creditResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<TransferExecutionResult>(creditResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new TransferExecutionResult(
            fromAccount.Id,
            toAccount.Id,
            fromAccount.Currency.ToString(),
            request.Amount));
    }
}
