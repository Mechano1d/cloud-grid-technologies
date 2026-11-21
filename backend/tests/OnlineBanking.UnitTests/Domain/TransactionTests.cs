using OnlineBanking.Module.Transactions.Domain;
using OnlineBanking.Shared.Results;

namespace OnlineBanking.UnitTests.Domain;

public sealed class TransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatePending_WithValidParameters_StartsInPendingState()
    {
        var result = Transaction.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ua213223130000026007233566001",
            250m,
            "uah",
            "  Rent payment  ",
            " key-1 ",
            Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(TransactionStatus.Pending, result.Value.Status);
        Assert.Equal(TransactionType.InternalTransfer, result.Value.Type);
        Assert.Equal("UA213223130000026007233566001", result.Value.ToIban);
        Assert.Equal("UAH", result.Value.Currency);
        Assert.Equal("Rent payment", result.Value.Description);
        Assert.Equal("key-1", result.Value.IdempotencyKey);
    }

    [Fact]
    public void CreatePending_SameSourceAndDestination_ReturnsValidationFailure()
    {
        var accountId = Guid.NewGuid();
        var result = Transaction.CreatePending(
            Guid.NewGuid(),
            accountId,
            accountId,
            "UA213223130000026007233566001",
            100m,
            "UAH",
            null,
            null,
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Transactions.SameAccount", result.Error.Code);
    }

    [Fact]
    public void MarkCompleted_FromPending_TransitionsToCompletedAndBlocksSecondTransition()
    {
        var tx = Transaction.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UA213223130000026007233566001",
            100m,
            "UAH",
            null,
            null,
            Now).Value;

        var completed = tx.MarkCompleted(Now.AddSeconds(1));
        var secondFail = tx.MarkFailed("Late error", Now.AddSeconds(2));

        Assert.True(completed.IsSuccess);
        Assert.Equal(TransactionStatus.Completed, tx.Status);
        Assert.True(secondFail.IsFailure);
        Assert.Equal(ErrorType.Conflict, secondFail.Error.Type);
    }
}
