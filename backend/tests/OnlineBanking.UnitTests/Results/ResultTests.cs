using OnlineBanking.Shared.Results;

namespace OnlineBanking.UnitTests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_WithValue_ExposesValueAndNoError()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Equal(DomainError.None, result.Error);
    }

    [Fact]
    public void Failure_WithDomainError_ThrowsWhenAccessingValue()
    {
        var error = new DomainError("Accounts.InsufficientFunds", "Insufficient funds.");
        var result = Result.Failure<decimal>(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
