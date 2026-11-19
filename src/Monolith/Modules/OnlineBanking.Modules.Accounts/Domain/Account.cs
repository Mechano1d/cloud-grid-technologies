using OnlineBanking.BuildingBlocks.Results;

namespace OnlineBanking.Modules.Accounts.Domain;

public enum AccountStatus
{
    Active = 1,
    Frozen = 2,
    Closed = 3
}

public enum Currency
{
    UAH = 1,
    USD = 2,
    EUR = 3
}

/// <summary>
/// Aggregate root owned exclusively by the Accounts module.
/// Only this entity mutates account balances (preserving single-ownership boundaries for Lab 3 extraction).
/// </summary>
public sealed class Account
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Iban { get; private set; } = string.Empty;
    public Currency Currency { get; private set; }
    public decimal Balance { get; private set; }
    public decimal BlockedAmount { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTimeOffset OpenedAtUtc { get; private set; }

    public decimal AvailableBalance => Balance - BlockedAmount;

    private Account() { }

    public static Result<Account> Open(
        Guid clientId,
        string iban,
        Currency currency,
        decimal initialDeposit,
        DateTimeOffset openedAtUtc)
    {
        if (clientId == Guid.Empty)
        {
            return Result.Failure<Account>(new DomainError("Accounts.InvalidClient", "Client ID must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(iban))
        {
            return Result.Failure<Account>(new DomainError("Accounts.InvalidIban", "IBAN must not be empty."));
        }

        if (initialDeposit < 0)
        {
            return Result.Failure<Account>(new DomainError("Accounts.NegativeDeposit", "Initial deposit cannot be negative."));
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Iban = iban,
            Currency = currency,
            Balance = initialDeposit,
            BlockedAmount = 0m,
            Status = AccountStatus.Active,
            OpenedAtUtc = openedAtUtc
        };

        return Result.Success(account);
    }

    public Result Debit(decimal amount)
    {
        if (Status != AccountStatus.Active)
        {
            return Result.Failure(new DomainError("Accounts.NotActive", "Cannot debit a non-active account."));
        }

        if (amount <= 0)
        {
            return Result.Failure(new DomainError("Accounts.InvalidAmount", "Debit amount must be positive."));
        }

        if (AvailableBalance < amount)
        {
            return Result.Failure(new DomainError("Accounts.InsufficientFunds", "Insufficient available balance."));
        }

        Balance -= amount;
        return Result.Success();
    }

    public Result Credit(decimal amount)
    {
        if (Status != AccountStatus.Active)
        {
            return Result.Failure(new DomainError("Accounts.NotActive", "Cannot credit a non-active account."));
        }

        if (amount <= 0)
        {
            return Result.Failure(new DomainError("Accounts.InvalidAmount", "Credit amount must be positive."));
        }

        Balance += amount;
        return Result.Success();
    }

    public Result Close()
    {
        if (Status == AccountStatus.Closed)
        {
            return Result.Failure(new DomainError("Accounts.AlreadyClosed", "Account is already closed."));
        }

        if (Balance != 0m || BlockedAmount != 0m)
        {
            return Result.Failure(new DomainError("Accounts.NonZeroBalance", "Account balance must be zero before closing."));
        }

        Status = AccountStatus.Closed;
        return Result.Success();
    }
}
