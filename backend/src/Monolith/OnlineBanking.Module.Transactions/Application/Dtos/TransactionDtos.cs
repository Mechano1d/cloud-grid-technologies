using System.ComponentModel.DataAnnotations;

namespace OnlineBanking.Module.Transactions.Application.Dtos;

public sealed record CreateTransferRequest(
    [Required] Guid FromAccountId,
    [Required, MaxLength(34)] string ToIban,
    [Required, Range(typeof(decimal), "0.01", "1000000000", ParseLimitsInInvariantCulture = true)] decimal Amount,
    [MaxLength(250)] string? Description);

public sealed record TransactionResponse(
    Guid Id,
    Guid FromAccountId,
    Guid ToAccountId,
    string ToIban,
    decimal Amount,
    string Currency,
    string Status,
    string Type,
    string? Description,
    string? IdempotencyKey,
    DateTimeOffset CreatedAtUtc);

public sealed record TransactionPageResponse(
    IReadOnlyList<TransactionResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
