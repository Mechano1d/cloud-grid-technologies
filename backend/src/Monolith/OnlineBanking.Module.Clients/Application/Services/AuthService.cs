using OnlineBanking.Infrastructure.Auth;
using OnlineBanking.Module.Clients.Application.Abstractions;
using OnlineBanking.Module.Clients.Application.Dtos;
using OnlineBanking.Module.Clients.Domain;
using OnlineBanking.Shared.Results;
using OnlineBanking.Shared.Time;

namespace OnlineBanking.Module.Clients.Application.Services;

public sealed class AuthService(
    IClientRepository clientRepository,
    IPasswordHasherService passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<Result<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return Result.Failure<AuthResponse>(DomainError.Validation(
                "Auth.WeakPassword",
                "Password must be at least 8 characters long."));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedTaxId = request.TaxId.Trim();

        if (await clientRepository.ExistsByEmailOrTaxIdAsync(normalizedEmail, normalizedTaxId, cancellationToken))
        {
            return Result.Failure<AuthResponse>(DomainError.Conflict(
                "Clients.AlreadyExists",
                "A client with the specified email or tax ID already exists."));
        }

        var passwordHash = passwordHasher.Hash(request.Password);
        var clientResult = Client.Create(
            request.FullName,
            normalizedEmail,
            request.Phone,
            request.BirthDate,
            normalizedTaxId,
            passwordHash,
            dateTimeProvider.UtcNow,
            ClientRole.Client);

        if (clientResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(clientResult.Error);
        }

        var client = clientResult.Value;
        await clientRepository.AddAsync(client, cancellationToken);
        await clientRepository.SaveChangesAsync(cancellationToken);

        var token = jwtTokenGenerator.Generate(client.Id, client.Email, client.Role.ToString());
        return Result.Success(new AuthResponse(
            token.AccessToken,
            token.ExpiresInSeconds,
            client.Id,
            client.Role.ToString()));
    }

    public async Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrEmpty(request.Password))
        {
            return Result.Failure<AuthResponse>(DomainError.Unauthorized(
                "Auth.InvalidCredentials",
                "Invalid email or password."));
        }

        var client = await clientRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (client is null || !passwordHasher.Verify(request.Password, client.PasswordHash))
        {
            return Result.Failure<AuthResponse>(DomainError.Unauthorized(
                "Auth.InvalidCredentials",
                "Invalid email or password."));
        }

        if (client.Status == ClientStatus.Blocked)
        {
            return Result.Failure<AuthResponse>(DomainError.Unauthorized(
                "Auth.ClientBlocked",
                "Client account is blocked."));
        }

        var token = jwtTokenGenerator.Generate(client.Id, client.Email, client.Role.ToString());
        return Result.Success(new AuthResponse(
            token.AccessToken,
            token.ExpiresInSeconds,
            client.Id,
            client.Role.ToString()));
    }
}
