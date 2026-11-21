using Microsoft.Extensions.Logging;
using OnlineBanking.Infrastructure.Auth;
using OnlineBanking.Module.Transactions.Application.Abstractions;
using OnlineBanking.Module.Transactions.Application.Dtos;
using OnlineBanking.Module.Transactions.Domain;
using OnlineBanking.Shared.Contracts;
using OnlineBanking.Shared.Results;
using OnlineBanking.Shared.Time;
using OnlineBanking.Shared.Validation;

namespace OnlineBanking.Module.Transactions.Application.Services;

public sealed partial class TransfersService(
    ITransactionRepository transactionRepository,
    IAccountsApi accountsApi,
    IClientsApi clientsApi,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<TransfersService> logger)
{
    public async Task<Result<TransactionResponse>> CreateTransferAsync(
        CreateTransferRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.TryGetClientId(out var clientId))
        {
            return Result.Failure<TransactionResponse>(DomainError.Unauthorized(
                "Auth.Unauthenticated",
                "Authentication is required."));
        }

        if (!await clientsApi.IsActiveClientAsync(clientId, cancellationToken))
        {
            return Result.Failure<TransactionResponse>(DomainError.Forbidden(
                "Clients.Inactive",
                "Client account is not active."));
        }

        var normalizedKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (normalizedKey is { Length: > 100 })
        {
            return Result.Failure<TransactionResponse>(DomainError.Validation(
                "Transactions.IdempotencyKeyTooLong",
                "Idempotency-Key must not exceed 100 characters."));
        }

        var normalizedToIban = request.ToIban?.Trim().ToUpperInvariant() ?? string.Empty;
        var normalizedDescription = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (normalizedKey is not null)
        {
            var existing = await transactionRepository.GetByIdempotencyKeyAsync(
                clientId,
                normalizedKey,
                cancellationToken);

            if (existing is not null)
            {
                return EvaluateIdempotentReplay(existing, request.FromAccountId, normalizedToIban, request.Amount, normalizedDescription);
            }
        }

        if (!MoneyValidation.IsValidPositiveAmount(request.Amount))
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

        if (fromAccount.ClientId != clientId)
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
            clientId,
            fromAccount.Id,
            toAccount.Id,
            toAccount.Iban,
            request.Amount,
            fromAccount.Currency,
            normalizedDescription,
            normalizedKey,
            dateTimeProvider.UtcNow);

        if (pendingResult.IsFailure)
        {
            return Result.Failure<TransactionResponse>(pendingResult.Error);
        }

        var transaction = pendingResult.Value;
        await transactionRepository.AddAsync(transaction, cancellationToken);
        if (!await transactionRepository.TrySaveChangesAsync(cancellationToken))
        {
            if (normalizedKey is not null)
            {
                var concurrentExisting = await transactionRepository.GetByIdempotencyKeyAsync(
                    clientId,
                    normalizedKey,
                    cancellationToken);

                if (concurrentExisting is not null)
                {
                    return EvaluateIdempotentReplay(
                        concurrentExisting,
                        request.FromAccountId,
                        normalizedToIban,
                        request.Amount,
                        normalizedDescription);
                }
            }

            return Result.Failure<TransactionResponse>(DomainError.Conflict(
                "Transactions.DuplicateRequest",
                "Duplicate transfer request."));
        }

        Result<TransferExecutionResult> executionResult;
        try
        {
            executionResult = await accountsApi.ExecuteTransferAsync(
                new TransferExecutionRequest(
                    transaction.Id,
                    fromAccount.Id,
                    toAccount.Id,
                    request.Amount,
                    fromAccount.Currency),
                cancellationToken);
        }
        catch (Exception ex)
        {
            var failureError = DomainError.Conflict(
                "Transactions.ExecutionFailed",
                "Transfer execution failed unexpectedly.");
            transaction.MarkFailed(failureError, dateTimeProvider.UtcNow);
            await transactionRepository.SaveChangesAsync(CancellationToken.None);
            LogTransferException(logger, ex, transaction.Id, fromAccount.Id, toAccount.Id);
            throw;
        }

        if (executionResult.IsFailure)
        {
            transaction.MarkFailed(executionResult.Error, dateTimeProvider.UtcNow);
            await transactionRepository.SaveChangesAsync(CancellationToken.None);
            LogTransferFailed(logger, transaction.Id, fromAccount.Id, toAccount.Id, executionResult.Error.Code);
            return Result.Failure<TransactionResponse>(executionResult.Error);
        }

        transaction.MarkCompleted(dateTimeProvider.UtcNow);
        await transactionRepository.SaveChangesAsync(CancellationToken.None);
        LogTransferCompleted(logger, transaction.Id, fromAccount.Id, toAccount.Id, request.Amount, fromAccount.Currency);

        return Result.Success(MapToResponse(transaction));
    }

    public async Task<Result<TransactionPageResponse>> ListAsync(
        Guid? accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.TryGetClientId(out var clientId))
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

        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            return Result.Failure<TransactionPageResponse>(DomainError.Validation(
                "Transactions.PageOverflow",
                "Requested page exceeds maximum pagination range."));
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

            if (!currentUser.IsAdmin && account.ClientId != clientId)
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
                : await accountsApi.GetAccountIdsByClientAsync(clientId, cancellationToken);
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
        if (!currentUser.TryGetClientId(out var clientId))
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
            var myAccountIds = await accountsApi.GetAccountIdsByClientAsync(clientId, cancellationToken);

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

    private static Result<TransactionResponse> EvaluateIdempotentReplay(
        Transaction existing,
        Guid fromAccountId,
        string normalizedToIban,
        decimal amount,
        string? normalizedDescription)
    {
        if (existing.FromAccountId != fromAccountId ||
            existing.Amount != amount ||
            !string.Equals(existing.ToIban, normalizedToIban, StringComparison.Ordinal) ||
            !string.Equals(existing.Description, normalizedDescription, StringComparison.Ordinal))
        {
            return Result.Failure<TransactionResponse>(DomainError.Conflict(
                "Transactions.IdempotencyKeyMismatch",
                "Idempotency-Key was already used with different transfer parameters."));
        }

        return existing.Status switch
        {
            TransactionStatus.Completed => Result.Success(MapToResponse(existing)),
            TransactionStatus.Failed => Result.Failure<TransactionResponse>(existing.GetFailureDomainError()),
            _ => Result.Failure<TransactionResponse>(DomainError.Conflict(
                "Transactions.InProgress",
                "Transfer is currently in progress."))
        };
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

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Transfer {TransactionId} completed from {FromAccountId} to {ToAccountId} for {Amount} {Currency}")]
    private static partial void LogTransferCompleted(
        ILogger logger,
        Guid transactionId,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string currency);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Warning,
        Message = "Transfer {TransactionId} failed from {FromAccountId} to {ToAccountId} with code {ErrorCode}")]
    private static partial void LogTransferFailed(
        ILogger logger,
        Guid transactionId,
        Guid fromAccountId,
        Guid toAccountId,
        string errorCode);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Error,
        Message = "Transfer {TransactionId} threw an unexpected exception from {FromAccountId} to {ToAccountId}")]
    private static partial void LogTransferException(
        ILogger logger,
        Exception exception,
        Guid transactionId,
        Guid fromAccountId,
        Guid toAccountId);
}
