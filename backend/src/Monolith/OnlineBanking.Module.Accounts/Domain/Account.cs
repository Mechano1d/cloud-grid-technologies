using OnlineBanking.Shared.Results;
using OnlineBanking.Shared.Validation;

namespace OnlineBanking.Module.Accounts.Domain;

public sealed class Account
{
    private Account()
    {
    }

    private Account(
        Guid id,
        Guid clientId,
        string iban,
        Currency currency,
        decimal balance,
        DateTimeOffset openedAtUtc)
    {
        Id = id;
        ClientId = clientId;
        Iban = iban;
        Currency = currency;
        Balance = balance;
        BlockedAmount = 0m;
        Status = AccountStatus.Active;
        OpenedAtUtc = openedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Iban { get; private set; } = string.Empty;
    public Currency Currency { get; private set; }
    public decimal Balance { get; private set; }
    public decimal BlockedAmount { get; private set; }
    public decimal AvailableBalance => Balance - BlockedAmount;
    public AccountStatus Status { get; private set; }
    public DateTimeOffset OpenedAtUtc { get; private set; }

    public static Result<Account> Open(
        Guid clientId,
        string iban,
        Currency currency,
        decimal initialDeposit,
        DateTimeOffset openedAtUtc,
        Guid? id = null)
    {
        if (clientId == Guid.Empty)
        {
            return Result.Failure<Account>(DomainError.Validation(
                "Accounts.InvalidClientId",
                "Client ID is required."));
        }

        var normalizedIban = iban?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!IbanGenerator.IsValidUaIban(normalizedIban))
        {
            return Result.Failure<Account>(DomainError.Validation(
                "Accounts.InvalidIban",
                "A valid Ukrainian IBAN is required."));
        }

        if (!MoneyValidation.IsValidNonNegativeAmount(initialDeposit))
        {
            return Result.Failure<Account>(DomainError.Validation(
                "Accounts.InvalidInitialDeposit",
                "Initial deposit must be non-negative with at most 2 decimal places."));
        }

        var account = new Account(
            id ?? Guid.NewGuid(),
            clientId,
            normalizedIban,
            currency,
            initialDeposit,
            openedAtUtc);

        return Result.Success(account);
    }

    public Result Debit(decimal amount)
    {
        if (Status != AccountStatus.Active)
        {
            return Result.Failure(DomainError.Conflict(
                "Accounts.NotActive",
                "Source account must be active to debit funds."));
        }

        if (!MoneyValidation.IsValidPositiveAmount(amount))
        {
            return Result.Failure(DomainError.Validation(
                "Accounts.InvalidAmount",
                "Debit amount must be greater than zero with at most 2 decimal places."));
        }

        if (AvailableBalance < amount)
        {
            return Result.Failure(DomainError.Conflict(
                "Accounts.InsufficientFunds",
                "Insufficient available funds on source account."));
        }

        Balance -= amount;
        return Result.Success();
    }

    public Result Credit(decimal amount)
    {
        if (Status != AccountStatus.Active)
        {
            return Result.Failure(DomainError.Conflict(
                "Accounts.NotActive",
                "Destination account must be active to credit funds."));
        }

        if (!MoneyValidation.IsValidPositiveAmount(amount))
        {
            return Result.Failure(DomainError.Validation(
                "Accounts.InvalidAmount",
                "Credit amount must be greater than zero with at most 2 decimal places."));
        }

        Balance += amount;
        return Result.Success();
    }

    public Result Close()
    {
        if (Status == AccountStatus.Closed)
        {
            return Result.Failure(DomainError.Conflict(
                "Accounts.AlreadyClosed",
                "Account is already closed."));
        }

        if (Balance != 0m || BlockedAmount != 0m)
        {
            return Result.Failure(DomainError.Conflict(
                "Accounts.NonZeroBalance",
                "Account balance and blocked funds must be zero before closing."));
        }

        Status = AccountStatus.Closed;
        return Result.Success();
    }
}
