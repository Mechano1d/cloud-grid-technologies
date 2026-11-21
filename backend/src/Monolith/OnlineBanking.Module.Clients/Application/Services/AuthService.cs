using Microsoft.Extensions.Logging;
using OnlineBanking.Infrastructure.Auth;
using OnlineBanking.Module.Clients.Application.Abstractions;
using OnlineBanking.Module.Clients.Application.Dtos;
using OnlineBanking.Module.Clients.Domain;
using OnlineBanking.Shared.Results;
using OnlineBanking.Shared.Time;

namespace OnlineBanking.Module.Clients.Application.Services;

public sealed partial class AuthService(
    IClientRepository clientRepository,
    IPasswordHasherService passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IDateTimeProvider dateTimeProvider,
    ILogger<AuthService> logger)
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
        if (!await clientRepository.TrySaveChangesAsync(cancellationToken))
        {
            return Result.Failure<AuthResponse>(DomainError.Conflict(
                "Clients.AlreadyExists",
                "A client with the specified email or tax ID already exists."));
        }

        LogClientRegistered(logger, client.Id, client.Role);

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
        var client = string.IsNullOrWhiteSpace(normalizedEmail)
            ? null
            : await clientRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        var passwordMatches = passwordHasher.Verify(
            request.Password ?? string.Empty,
            client?.PasswordHash ?? passwordHasher.DummyHash);

        if (client is null || !passwordMatches || string.IsNullOrEmpty(request.Password))
        {
            LogLoginFailed(logger, client?.Id);
            return Result.Failure<AuthResponse>(DomainError.Unauthorized(
                "Auth.InvalidCredentials",
                "Invalid email or password."));
        }

        if (client.Status == ClientStatus.Blocked)
        {
            LogBlockedLoginAttempt(logger, client.Id);
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

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Client {ClientId} registered with role {Role}")]
    private static partial void LogClientRegistered(ILogger logger, Guid clientId, ClientRole role);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "Failed login attempt for client {ClientId}")]
    private static partial void LogLoginFailed(ILogger logger, Guid? clientId);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Blocked client {ClientId} attempted to log in")]
    private static partial void LogBlockedLoginAttempt(ILogger logger, Guid clientId);
}
