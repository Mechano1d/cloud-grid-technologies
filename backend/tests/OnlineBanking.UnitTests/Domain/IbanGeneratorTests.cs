using OnlineBanking.Module.Accounts.Domain;

namespace OnlineBanking.UnitTests.Domain;

public sealed class IbanGeneratorTests
{
    [Fact]
    public void Generate_ReturnsValid29CharacterUaIban()
    {
        var iban = IbanGenerator.Generate();

        Assert.Equal(29, iban.Length);
        Assert.StartsWith("UA", iban, StringComparison.Ordinal);
        Assert.True(IbanGenerator.IsValidUaIban(iban));
    }

    [Fact]
    public void CreateFromBban_MatchesKnownValidExample()
    {
        var iban = IbanGenerator.CreateFromBban("322313", "0000026007233566001");

        Assert.Equal("UA213223130000026007233566001", iban);
        Assert.True(IbanGenerator.IsValidUaIban(iban));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DE89370400440532013000")]
    [InlineData("UA003223130000026007233566001")]
    [InlineData("UA21322313000002600723356600")]
    public void IsValidUaIban_WithInvalidInputs_ReturnsFalse(string? iban)
    {
        Assert.False(IbanGenerator.IsValidUaIban(iban));
    }
}
