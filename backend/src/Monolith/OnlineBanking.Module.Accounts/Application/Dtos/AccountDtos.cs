using System.ComponentModel.DataAnnotations;

namespace OnlineBanking.Module.Accounts.Application.Dtos;

public sealed record OpenAccountRequest(
    string? Currency = "UAH",
    [Range(typeof(decimal), "0", "1000000000")] decimal InitialDeposit = 0m);

public sealed record AccountResponse(
    Guid Id,
    Guid ClientId,
    string Iban,
    string Currency,
    decimal Balance,
    decimal BlockedAmount,
    decimal AvailableBalance,
    string Status,
    DateTimeOffset OpenedAtUtc);
