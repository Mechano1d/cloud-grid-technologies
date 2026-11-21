using System.ComponentModel.DataAnnotations;

namespace OnlineBanking.Module.Clients.Application.Dtos;

public sealed record RegisterRequest(
    [Required, MinLength(2), MaxLength(200)] string FullName,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(30)] string Phone,
    [Required] DateOnly BirthDate,
    [Required, MinLength(8), MaxLength(20)] string TaxId,
    [Required, MinLength(8), MaxLength(128)] string Password);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record AuthResponse(
    string AccessToken,
    int ExpiresInSeconds,
    Guid ClientId,
    string Role);

public sealed record ClientResponse(
    Guid Id,
    string FullName,
    string Email,
    string Phone,
    DateOnly BirthDate,
    string TaxId,
    string Role,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record UpdateClientStatusRequest(
    [Required] string Status,
    [MaxLength(500)] string? Reason);
