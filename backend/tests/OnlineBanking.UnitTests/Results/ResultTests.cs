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
        Assert.Equal(ErrorType.None, result.Error.Type);
    }

    [Fact]
    public void Failure_WithDomainError_ThrowsWhenAccessingValue()
    {
        var error = new DomainError("Accounts.InsufficientFunds", "Insufficient funds.");
        var result = Result.Failure<decimal>(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    public void DomainError_FactoryHelpers_SetExpectedErrorType(ErrorType expectedType)
    {
        DomainError error = expectedType switch
        {
            ErrorType.Validation => DomainError.Validation("Test.Validation", "Validation failed."),
            ErrorType.Unauthorized => DomainError.Unauthorized("Test.Unauthorized", "Unauthorized."),
            ErrorType.Forbidden => DomainError.Forbidden("Test.Forbidden", "Forbidden."),
            ErrorType.NotFound => DomainError.NotFound("Test.NotFound", "Not found."),
            ErrorType.Conflict => DomainError.Conflict("Test.Conflict", "Conflict."),
            _ => throw new ArgumentOutOfRangeException(nameof(expectedType))
        };

        Assert.Equal(expectedType, error.Type);
    }
}
