using OnlineBanking.Shared.Results;

namespace OnlineBanking.Module.Transactions.Domain;

public sealed class Transaction
{
    private Transaction()
    {
    }

    private Transaction(
        Guid id,
        Guid initiatedByClientId,
        Guid fromAccountId,
        Guid toAccountId,
        string toIban,
        decimal amount,
        string currency,
        string? description,
        string? idempotencyKey,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        InitiatedByClientId = initiatedByClientId;
        FromAccountId = fromAccountId;
        ToAccountId = toAccountId;
        ToIban = toIban;
        Amount = amount;
        Currency = currency;
        Status = TransactionStatus.Pending;
        Type = TransactionType.InternalTransfer;
        Description = description;
        IdempotencyKey = idempotencyKey;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid InitiatedByClientId { get; private set; }
    public Guid FromAccountId { get; private set; }
    public Guid ToAccountId { get; private set; }
    public string ToIban { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public TransactionStatus Status { get; private set; }
    public TransactionType Type { get; private set; }
    public string? Description { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static Result<Transaction> CreatePending(
        Guid initiatedByClientId,
        Guid fromAccountId,
        Guid toAccountId,
        string toIban,
        decimal amount,
        string currency,
        string? description,
        string? idempotencyKey,
        DateTimeOffset createdAtUtc,
        Guid? id = null)
    {
        if (initiatedByClientId == Guid.Empty || fromAccountId == Guid.Empty || toAccountId == Guid.Empty)
        {
            return Result.Failure<Transaction>(DomainError.Validation(
                "Transactions.InvalidParticipants",
                "Initiator, source, and destination account identifiers are required."));
        }

        if (fromAccountId == toAccountId)
        {
            return Result.Failure<Transaction>(DomainError.Validation(
                "Transactions.SameAccount",
                "Source and destination accounts must be different."));
        }

        if (string.IsNullOrWhiteSpace(toIban))
        {
            return Result.Failure<Transaction>(DomainError.Validation(
                "Transactions.InvalidDestinationIban",
                "Destination IBAN is required."));
        }

        if (amount <= 0m || decimal.Round(amount, 2) != amount)
        {
            return Result.Failure<Transaction>(DomainError.Validation(
                "Transactions.InvalidAmount",
                "Transfer amount must be greater than zero with at most 2 decimal places."));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result.Failure<Transaction>(DomainError.Validation(
                "Transactions.InvalidCurrency",
                "Currency is required."));
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is { Length: > 250 })
        {
            return Result.Failure<Transaction>(DomainError.Validation(
                "Transactions.DescriptionTooLong",
                "Description must not exceed 250 characters."));
        }

        var trimmedKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (trimmedKey is { Length: > 100 })
        {
            return Result.Failure<Transaction>(DomainError.Validation(
                "Transactions.IdempotencyKeyTooLong",
                "Idempotency-Key must not exceed 100 characters."));
        }

        var transaction = new Transaction(
            id ?? Guid.NewGuid(),
            initiatedByClientId,
            fromAccountId,
            toAccountId,
            toIban.Trim().ToUpperInvariant(),
            amount,
            currency.Trim().ToUpperInvariant(),
            trimmedDescription,
            trimmedKey,
            createdAtUtc);

        return Result.Success(transaction);
    }

    public Result MarkCompleted(DateTimeOffset completedAtUtc)
    {
        if (Status != TransactionStatus.Pending)
        {
            return Result.Failure(DomainError.Conflict(
                "Transactions.InvalidStateTransition",
                "Only pending transactions can be marked as completed."));
        }

        Status = TransactionStatus.Completed;
        CompletedAtUtc = completedAtUtc;
        return Result.Success();
    }

    public Result MarkFailed(string reason, DateTimeOffset completedAtUtc)
    {
        if (Status != TransactionStatus.Pending)
        {
            return Result.Failure(DomainError.Conflict(
                "Transactions.InvalidStateTransition",
                "Only pending transactions can be marked as failed."));
        }

        Status = TransactionStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Transfer failed." : reason.Trim();
        CompletedAtUtc = completedAtUtc;
        return Result.Success();
    }
}
