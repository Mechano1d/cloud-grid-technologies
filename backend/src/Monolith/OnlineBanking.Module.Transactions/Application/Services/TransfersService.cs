using OnlineBanking.Infrastructure.Auth;
using OnlineBanking.Module.Transactions.Application.Abstractions;
using OnlineBanking.Module.Transactions.Application.Dtos;
using OnlineBanking.Module.Transactions.Domain;
using OnlineBanking.Shared.Contracts;
using OnlineBanking.Shared.Results;
using OnlineBanking.Shared.Time;

namespace OnlineBanking.Module.Transactions.Application.Services;

public sealed class TransfersService(
    ITransactionRepository transactionRepository,
    IAccountsApi accountsApi,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<Result<TransactionResponse>> CreateTransferAsync(
        CreateTransferRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.ClientId.HasValue)
        {
            return Result.Failure<TransactionResponse>(DomainError.Unauthorized(
                "Auth.Unauthenticated",
                "Authentication is required."));
        }

        var normalizedKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (normalizedKey is { Length: > 100 })
        {
            return Result.Failure<TransactionResponse>(DomainError.Validation(
                "Transactions.IdempotencyKeyTooLong",
                "Idempotency-Key must not exceed 100 characters."));
        }

        var normalizedToIban = request.ToIban?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalizedKey is not null)
        {
            var existing = await transactionRepository.GetByIdempotencyKeyAsync(
                currentUser.ClientId.Value,
                normalizedKey,
                cancellationToken);

            if (existing is not null)
            {
                if (existing.FromAccountId != request.FromAccountId ||
                    existing.Amount != request.Amount ||
                    !string.Equals(existing.ToIban, normalizedToIban, StringComparison.Ordinal))
                {
                    return Result.Failure<TransactionResponse>(DomainError.Conflict(
                        "Transactions.IdempotencyKeyMismatch",
                        "Idempotency-Key was already used with different transfer parameters."));
                }

                return existing.Status switch
                {
                    TransactionStatus.Completed => Result.Success(MapToResponse(existing)),
                    TransactionStatus.Failed => Result.Failure<TransactionResponse>(DomainError.Conflict(
                        "Transactions.TransferFailed",
                        existing.FailureReason ?? "Transfer failed.")),
                    _ => Result.Failure<TransactionResponse>(DomainError.Conflict(
                        "Transactions.InProgress",
                        "Transfer is currently in progress."))
                };
            }
        }

        if (request.Amount <= 0m || decimal.Round(request.Amount, 2) != request.Amount)
        {
            return Result.Failure<TransactionResponse>(DomainError.Validation(
                "Transactions.InvalidAmount",
                "Transfer amount must be greater than zero with at most 2 decimal places."));
        }

        var fromAccount = await accountsApi.GetByIdAsync(request.FromAccountId, cancellationToken);
        if (fromAccount is null)
        {
            return Result.Failure<TransactionResponse>(DomainError.NotFound(
                "Accounts.SourceNotFound",
                "Source account was not found."));
        }

        if (!currentUser.IsAdmin && fromAccount.ClientId != currentUser.ClientId.Value)
        {
            return Result.Failure<TransactionResponse>(DomainError.Forbidden(
                "Accounts.Forbidden",
                "You do not own the source account."));
        }

        var toAccount = await accountsApi.GetByIbanAsync(normalizedToIban, cancellationToken);
        if (toAccount is null)
        {
            return Result.Failure<TransactionResponse>(DomainError.NotFound(
                "Accounts.DestinationNotFound",
                "Destination account for the specified IBAN was not found."));
        }

        var pendingResult = Transaction.CreatePending(
            currentUser.ClientId.Value,
            fromAccount.Id,
            toAccount.Id,
            toAccount.Iban,
            request.Amount,
            fromAccount.Currency,
            request.Description,
            normalizedKey,
            dateTimeProvider.UtcNow);

        if (pendingResult.IsFailure)
        {
            return Result.Failure<TransactionResponse>(pendingResult.Error);
        }

        var transaction = pendingResult.Value;
        await transactionRepository.AddAsync(transaction, cancellationToken);
        await transactionRepository.SaveChangesAsync(cancellationToken);

        var executionResult = await accountsApi.ExecuteTransferAsync(
            new TransferExecutionRequest(
                fromAccount.Id,
                toAccount.Id,
                request.Amount,
                fromAccount.Currency),
            cancellationToken);

        if (executionResult.IsFailure)
        {
            transaction.MarkFailed(executionResult.Error.Message, dateTimeProvider.UtcNow);
            await transactionRepository.SaveChangesAsync(cancellationToken);
            return Result.Failure<TransactionResponse>(executionResult.Error);
        }

        transaction.MarkCompleted(dateTimeProvider.UtcNow);
        await transactionRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToResponse(transaction));
    }

    public async Task<Result<TransactionPageResponse>> ListAsync(
        Guid? accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.ClientId.HasValue)
        {
            return Result.Failure<TransactionPageResponse>(DomainError.Unauthorized(
                "Auth.Unauthenticated",
                "Authentication is required."));
        }

        if (page < 1)
        {
            return Result.Failure<TransactionPageResponse>(DomainError.Validation(
                "Transactions.InvalidPage",
                "Page must be at least 1."));
        }

        if (pageSize is < 1 or > 100)
        {
            return Result.Failure<TransactionPageResponse>(DomainError.Validation(
                "Transactions.InvalidPageSize",
                "Page size must be between 1 and 100."));
        }

        IReadOnlyCollection<Guid>? filterAccountIds;
        if (accountId.HasValue)
        {
            var account = await accountsApi.GetByIdAsync(accountId.Value, cancellationToken);
            if (account is null)
            {
                return Result.Failure<TransactionPageResponse>(DomainError.NotFound(
                    "Accounts.NotFound",
                    "Account was not found."));
            }

            if (!currentUser.IsAdmin && account.ClientId != currentUser.ClientId.Value)
            {
                return Result.Failure<TransactionPageResponse>(DomainError.Forbidden(
                    "Accounts.Forbidden",
                    "You do not have access to this account's transactions."));
            }

            filterAccountIds = [accountId.Value];
        }
        else
        {
            filterAccountIds = currentUser.IsAdmin
                ? null
                : await accountsApi.GetAccountIdsByClientAsync(currentUser.ClientId.Value, cancellationToken);
        }

        var (items, totalCount) = await transactionRepository.ListPageAsync(
            filterAccountIds,
            page,
            pageSize,
            cancellationToken);

        return Result.Success(new TransactionPageResponse(
            items.Select(MapToResponse).ToList(),
            page,
            pageSize,
            totalCount));
    }

    public async Task<Result<TransactionResponse>> GetByIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.ClientId.HasValue)
        {
            return Result.Failure<TransactionResponse>(DomainError.Unauthorized(
                "Auth.Unauthenticated",
                "Authentication is required."));
        }

        var transaction = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (transaction is null)
        {
            return Result.Failure<TransactionResponse>(DomainError.NotFound(
                "Transactions.NotFound",
                "Transaction was not found."));
        }

        if (!currentUser.IsAdmin)
        {
            var myAccountIds = await accountsApi.GetAccountIdsByClientAsync(
                currentUser.ClientId.Value,
                cancellationToken);

            if (!myAccountIds.Contains(transaction.FromAccountId) &&
                !myAccountIds.Contains(transaction.ToAccountId))
            {
                return Result.Failure<TransactionResponse>(DomainError.Forbidden(
                    "Transactions.Forbidden",
                    "You do not have access to this transaction."));
            }
        }

        return Result.Success(MapToResponse(transaction));
    }

    private static TransactionResponse MapToResponse(Transaction transaction) =>
        new(
            transaction.Id,
            transaction.FromAccountId,
            transaction.ToAccountId,
            transaction.ToIban,
            transaction.Amount,
            transaction.Currency,
            transaction.Status.ToString(),
            transaction.Type.ToString(),
            transaction.Description,
            transaction.IdempotencyKey,
            transaction.CreatedAtUtc);
}
