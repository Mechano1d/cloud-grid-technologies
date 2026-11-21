using OnlineBanking.Infrastructure.Auth;
using OnlineBanking.Module.Accounts.Application.Abstractions;
using OnlineBanking.Module.Accounts.Application.Dtos;
using OnlineBanking.Module.Accounts.Domain;
using OnlineBanking.Shared.Contracts;
using OnlineBanking.Shared.Results;
using OnlineBanking.Shared.Time;

namespace OnlineBanking.Module.Accounts.Application.Services;

public sealed class AccountsService(
    IAccountRepository accountRepository,
    IClientsApi clientsApi,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<Result<IReadOnlyList<AccountResponse>>> ListMyAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.ClientId.HasValue)
        {
            return Result.Failure<IReadOnlyList<AccountResponse>>(DomainError.Unauthorized(
                "Auth.Unauthenticated",
                "Authentication is required."));
        }

        var accounts = await accountRepository.ListByClientIdAsync(currentUser.ClientId.Value, cancellationToken);
        IReadOnlyList<AccountResponse> response = accounts.Select(MapToResponse).ToList();
        return Result.Success(response);
    }

    public async Task<Result<AccountResponse>> OpenAccountAsync(
        OpenAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.ClientId.HasValue)
        {
            return Result.Failure<AccountResponse>(DomainError.Unauthorized(
                "Auth.Unauthenticated",
                "Authentication is required."));
        }

        if (!await clientsApi.IsActiveClientAsync(currentUser.ClientId.Value, cancellationToken))
        {
            return Result.Failure<AccountResponse>(DomainError.Forbidden(
                "Clients.Inactive",
                "Client account is not active."));
        }

        var currencyText = string.IsNullOrWhiteSpace(request.Currency) ? nameof(Currency.UAH) : request.Currency.Trim();
        Currency? parsedCurrency = currencyText switch
        {
            nameof(Currency.UAH) => Currency.UAH,
            nameof(Currency.USD) => Currency.USD,
            nameof(Currency.EUR) => Currency.EUR,
            _ => null
        };

        if (parsedCurrency is not { } currency)
        {
            return Result.Failure<AccountResponse>(DomainError.Validation(
                "Accounts.InvalidCurrency",
                "Currency must be one of: UAH, USD, EUR."));
        }

        var iban = await GenerateUniqueIbanAsync(cancellationToken);
        var accountResult = Account.Open(
            currentUser.ClientId.Value,
            iban,
            currency,
            request.InitialDeposit,
            dateTimeProvider.UtcNow);

        if (accountResult.IsFailure)
        {
            return Result.Failure<AccountResponse>(accountResult.Error);
        }

        var account = accountResult.Value;
        await accountRepository.AddAsync(account, cancellationToken);
        await accountRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToResponse(account));
    }

    public async Task<Result<AccountResponse>> GetByIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.ClientId.HasValue)
        {
            return Result.Failure<AccountResponse>(DomainError.Unauthorized(
                "Auth.Unauthenticated",
                "Authentication is required."));
        }

        var account = await accountRepository.GetByIdAsNoTrackingAsync(accountId, cancellationToken);
        if (account is null)
        {
            return Result.Failure<AccountResponse>(DomainError.NotFound(
                "Accounts.NotFound",
                "Account was not found."));
        }

        if (!currentUser.IsAdmin && account.ClientId != currentUser.ClientId.Value)
        {
            return Result.Failure<AccountResponse>(DomainError.Forbidden(
                "Accounts.Forbidden",
                "You do not have access to this account."));
        }

        return Result.Success(MapToResponse(account));
    }

    public async Task<Result<AccountResponse>> CloseAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.ClientId.HasValue)
        {
            return Result.Failure<AccountResponse>(DomainError.Unauthorized(
                "Auth.Unauthenticated",
                "Authentication is required."));
        }

        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return Result.Failure<AccountResponse>(DomainError.NotFound(
                "Accounts.NotFound",
                "Account was not found."));
        }

        if (!currentUser.IsAdmin && account.ClientId != currentUser.ClientId.Value)
        {
            return Result.Failure<AccountResponse>(DomainError.Forbidden(
                "Accounts.Forbidden",
                "You do not have permission to close this account."));
        }

        var closeResult = account.Close();
        if (closeResult.IsFailure)
        {
            return Result.Failure<AccountResponse>(closeResult.Error);
        }

        await accountRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(MapToResponse(account));
    }

    private async Task<string> GenerateUniqueIbanAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = IbanGenerator.Generate();
            if (!await accountRepository.ExistsByIbanAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique IBAN after 5 attempts.");
    }

    private static AccountResponse MapToResponse(Account account) =>
        new(
            account.Id,
            account.ClientId,
            account.Iban,
            account.Currency.ToString(),
            account.Balance,
            account.BlockedAmount,
            account.AvailableBalance,
            account.Status.ToString(),
            account.OpenedAtUtc);
}
