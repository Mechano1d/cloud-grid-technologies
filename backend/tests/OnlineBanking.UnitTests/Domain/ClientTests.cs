using OnlineBanking.Module.Clients.Domain;
using OnlineBanking.Shared.Results;

namespace OnlineBanking.UnitTests.Domain;

public sealed class ClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_NormalizesEmailAndDefaultsToActiveClient()
    {
        var result = Client.Create(
            "  Ivan Petrenko  ",
            "  Ivan.Petrenko@Example.COM ",
            " +380671234567 ",
            new DateOnly(1999, 5, 10),
            " 1234567890 ",
            "hashed-password",
            Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ivan Petrenko", result.Value.FullName);
        Assert.Equal("ivan.petrenko@example.com", result.Value.Email);
        Assert.Equal("+380671234567", result.Value.Phone);
        Assert.Equal("1234567890", result.Value.TaxId);
        Assert.Equal(ClientRole.Client, result.Value.Role);
        Assert.Equal(ClientStatus.Active, result.Value.Status);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("")]
    public void Create_WithInvalidFullName_ReturnsValidationFailure(string fullName)
    {
        var result = Client.Create(
            fullName,
            "user@example.com",
            "+380671234567",
            new DateOnly(1999, 5, 10),
            "1234567890",
            "hashed-password",
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Clients.InvalidFullName", result.Error.Code);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("")]
    public void Create_WithInvalidEmail_ReturnsValidationFailure(string email)
    {
        var result = Client.Create(
            "Ivan Petrenko",
            email,
            "+380671234567",
            new DateOnly(1999, 5, 10),
            "1234567890",
            "hashed-password",
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Clients.InvalidEmail", result.Error.Code);
    }

    [Theory]
    [InlineData(2099, 1, 1)]
    [InlineData(2020, 1, 1)]
    [InlineData(1850, 1, 1)]
    public void Create_WithOutOfRangeBirthDate_ReturnsValidationFailure(int year, int month, int day)
    {
        var result = Client.Create(
            "Ivan Petrenko",
            "ivan@example.com",
            "+380671234567",
            new DateOnly(year, month, day),
            "1234567890",
            "hashed-password",
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Clients.InvalidBirthDate", result.Error.Code);
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("12345ABC90")]
    [InlineData("123456789012345678901")]
    public void Create_WithInvalidTaxId_ReturnsValidationFailure(string taxId)
    {
        var result = Client.Create(
            "Ivan Petrenko",
            "ivan@example.com",
            "+380671234567",
            new DateOnly(1999, 5, 10),
            taxId,
            "hashed-password",
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Clients.InvalidTaxId", result.Error.Code);
    }

    [Fact]
    public void UpdateStatus_ToBlocked_UpdatesStatusAndReason()
    {
        var client = Client.Create(
            "Ivan Petrenko",
            "ivan@example.com",
            "+380671234567",
            new DateOnly(1999, 5, 10),
            "1234567890",
            "hashed-password",
            Now).Value;

        var update = client.UpdateStatus(ClientStatus.Blocked, "  Suspicious activity ");

        Assert.True(update.IsSuccess);
        Assert.Equal(ClientStatus.Blocked, client.Status);
        Assert.Equal("Suspicious activity", client.StatusReason);
    }
}
