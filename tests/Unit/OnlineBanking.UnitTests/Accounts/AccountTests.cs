using OnlineBanking.Modules.Accounts.Domain;

namespace OnlineBanking.UnitTests.Accounts;

public sealed class AccountTests
{
    [Fact]
    public void Open_WithValidParameters_CreatesActiveAccount()
    {
        var clientId = Guid.NewGuid();
        var iban = IbanGenerator.GenerateUkrainianIban();
        var now = DateTimeOffset.UtcNow;

        var result = Account.Open(clientId, iban, Currency.UAH, 1000m, now);

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, result.Value.Balance);
        Assert.Equal(1000m, result.Value.AvailableBalance);
        Assert.Equal(AccountStatus.Active, result.Value.Status);
    }

    [Fact]
    public void Debit_WhenInsufficientFunds_ReturnsFailureWithoutChangingBalance()
    {
        var account = Account.Open(Guid.NewGuid(), IbanGenerator.GenerateUkrainianIban(), Currency.UAH, 250m, DateTimeOffset.UtcNow).Value;

        var result = account.Debit(300m);

        Assert.True(result.IsFailure);
        Assert.Equal("Accounts.InsufficientFunds", result.Error.Code);
        Assert.Equal(250m, account.Balance);
    }

    [Fact]
    public void DebitAndCredit_WithValidAmounts_UpdatesBalances()
    {
        var sender = Account.Open(Guid.NewGuid(), IbanGenerator.GenerateUkrainianIban(), Currency.UAH, 500m, DateTimeOffset.UtcNow).Value;
        var recipient = Account.Open(Guid.NewGuid(), IbanGenerator.GenerateUkrainianIban(), Currency.UAH, 100m, DateTimeOffset.UtcNow).Value;

        Assert.True(sender.Debit(200m).IsSuccess);
        Assert.True(recipient.Credit(200m).IsSuccess);
        Assert.Equal(300m, sender.Balance);
        Assert.Equal(300m, recipient.Balance);
    }

    [Fact]
    public void Close_WhenBalanceIsNonZero_ReturnsFailure()
    {
        var account = Account.Open(Guid.NewGuid(), IbanGenerator.GenerateUkrainianIban(), Currency.UAH, 50m, DateTimeOffset.UtcNow).Value;

        var result = account.Close();

        Assert.True(result.IsFailure);
        Assert.Equal("Accounts.NonZeroBalance", result.Error.Code);
        Assert.Equal(AccountStatus.Active, account.Status);
    }

    [Fact]
    public void Close_WhenBalanceIsZero_TransitionsToClosed()
    {
        var account = Account.Open(Guid.NewGuid(), IbanGenerator.GenerateUkrainianIban(), Currency.UAH, 0m, DateTimeOffset.UtcNow).Value;

        var result = account.Close();

        Assert.True(result.IsSuccess);
        Assert.Equal(AccountStatus.Closed, account.Status);
    }

    [Fact]
    public void GenerateUkrainianIban_ProducesValid29CharUaIban()
    {
        var iban = IbanGenerator.GenerateUkrainianIban();

        Assert.Equal(29, iban.Length);
        Assert.StartsWith("UA", iban, StringComparison.Ordinal);
        Assert.All(iban[2..], c => Assert.True(char.IsDigit(c)));
    }
}
