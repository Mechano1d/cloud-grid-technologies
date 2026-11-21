using OnlineBanking.Module.Accounts.Domain;
using OnlineBanking.Shared.Results;

namespace OnlineBanking.UnitTests.Domain;

public sealed class AccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);
    private const string ValidIban = "UA213223130000026007233566001";

    [Fact]
    public void Open_WithValidInput_CreatesActiveAccount()
    {
        var clientId = Guid.NewGuid();
        var result = Account.Open(clientId, ValidIban, Currency.UAH, 1000.50m, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(clientId, result.Value.ClientId);
        Assert.Equal(ValidIban, result.Value.Iban);
        Assert.Equal(1000.50m, result.Value.Balance);
        Assert.Equal(1000.50m, result.Value.AvailableBalance);
        Assert.Equal(AccountStatus.Active, result.Value.Status);
    }

    [Fact]
    public void Debit_WithSufficientFunds_DecreasesBalance()
    {
        var account = Account.Open(Guid.NewGuid(), ValidIban, Currency.UAH, 500m, Now).Value;

        var result = account.Debit(200m);

        Assert.True(result.IsSuccess);
        Assert.Equal(300m, account.Balance);
    }

    [Fact]
    public void Debit_ExceedingAvailableBalance_ReturnsConflict()
    {
        var account = Account.Open(Guid.NewGuid(), ValidIban, Currency.UAH, 100m, Now).Value;

        var result = account.Debit(150m);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Accounts.InsufficientFunds", result.Error.Code);
        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void Credit_OnActiveAccount_IncreasesBalance()
    {
        var account = Account.Open(Guid.NewGuid(), ValidIban, Currency.UAH, 100m, Now).Value;

        var result = account.Credit(250.25m);

        Assert.True(result.IsSuccess);
        Assert.Equal(350.25m, account.Balance);
    }

    [Fact]
    public void Close_WithNonZeroBalance_ReturnsConflict()
    {
        var account = Account.Open(Guid.NewGuid(), ValidIban, Currency.UAH, 10m, Now).Value;

        var result = account.Close();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Accounts.NonZeroBalance", result.Error.Code);
        Assert.Equal(AccountStatus.Active, account.Status);
    }

    [Fact]
    public void Close_WithZeroBalance_ClosesAccountAndRejectsFurtherOperations()
    {
        var account = Account.Open(Guid.NewGuid(), ValidIban, Currency.UAH, 0m, Now).Value;

        var closeResult = account.Close();

        Assert.True(closeResult.IsSuccess);
        Assert.Equal(AccountStatus.Closed, account.Status);
        Assert.True(account.Debit(1m).IsFailure);
        Assert.True(account.Credit(1m).IsFailure);
        Assert.Equal("Accounts.AlreadyClosed", account.Close().Error.Code);
    }
}
