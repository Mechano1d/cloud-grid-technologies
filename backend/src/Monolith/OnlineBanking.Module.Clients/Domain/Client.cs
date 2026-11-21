using OnlineBanking.Shared.Results;

namespace OnlineBanking.Module.Clients.Domain;

public sealed class Client
{
    private Client()
    {
    }

    private Client(
        Guid id,
        string fullName,
        string email,
        string phone,
        DateOnly birthDate,
        string taxId,
        string passwordHash,
        ClientRole role,
        ClientStatus status,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        FullName = fullName;
        Email = email;
        Phone = phone;
        BirthDate = birthDate;
        TaxId = taxId;
        PasswordHash = passwordHash;
        Role = role;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public DateOnly BirthDate { get; private set; }
    public string TaxId { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public ClientRole Role { get; private set; }
    public ClientStatus Status { get; private set; }
    public string? StatusReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<Client> Create(
        string fullName,
        string email,
        string phone,
        DateOnly birthDate,
        string taxId,
        string passwordHash,
        DateTimeOffset createdAtUtc,
        ClientRole role = ClientRole.Client,
        Guid? id = null)
    {
        var trimmedName = fullName?.Trim() ?? string.Empty;
        if (trimmedName.Length is < 2 or > 200)
        {
            return Result.Failure<Client>(DomainError.Validation(
                "Clients.InvalidFullName",
                "Full name must be between 2 and 200 characters."));
        }

        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@', StringComparison.Ordinal))
        {
            return Result.Failure<Client>(DomainError.Validation(
                "Clients.InvalidEmail",
                "A valid email address is required."));
        }

        var trimmedPhone = phone?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedPhone) || trimmedPhone.Length > 30)
        {
            return Result.Failure<Client>(DomainError.Validation(
                "Clients.InvalidPhone",
                "Phone number must be between 1 and 30 characters."));
        }

        var trimmedTaxId = taxId?.Trim() ?? string.Empty;
        if (trimmedTaxId.Length is < 8 or > 20)
        {
            return Result.Failure<Client>(DomainError.Validation(
                "Clients.InvalidTaxId",
                "Tax ID must be between 8 and 20 characters."));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<Client>(DomainError.Validation(
                "Clients.InvalidPasswordHash",
                "Password hash cannot be empty."));
        }

        var client = new Client(
            id ?? Guid.NewGuid(),
            trimmedName,
            normalizedEmail,
            trimmedPhone,
            birthDate,
            trimmedTaxId,
            passwordHash,
            role,
            ClientStatus.Active,
            createdAtUtc);

        return Result.Success(client);
    }

    public Result UpdateStatus(ClientStatus status, string? reason = null)
    {
        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmedReason is { Length: > 500 })
        {
            return Result.Failure(DomainError.Validation(
                "Clients.InvalidStatusReason",
                "Status reason must not exceed 500 characters."));
        }

        Status = status;
        StatusReason = trimmedReason;
        return Result.Success();
    }
}
