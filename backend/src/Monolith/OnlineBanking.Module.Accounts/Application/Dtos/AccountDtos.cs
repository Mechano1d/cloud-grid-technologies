using System.ComponentModel.DataAnnotations;

namespace OnlineBanking.Module.Accounts.Application.Dtos;

public sealed record OpenAccountRequest(
    string? Currency = "UAH",
    [Range(0, 1_000_000_000)] decimal InitialDeposit = 0m);

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
