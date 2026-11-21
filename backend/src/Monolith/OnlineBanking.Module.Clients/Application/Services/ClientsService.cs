using OnlineBanking.Infrastructure.Auth;
using OnlineBanking.Module.Clients.Application.Abstractions;
using OnlineBanking.Module.Clients.Application.Dtos;
using OnlineBanking.Module.Clients.Domain;
using OnlineBanking.Shared.Results;

namespace OnlineBanking.Module.Clients.Application.Services;

public sealed class ClientsService(
    IClientRepository clientRepository,
    ICurrentUser currentUser)
{
    public async Task<Result<ClientResponse>> GetMeAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.ClientId.HasValue)
        {
            return Result.Failure<ClientResponse>(DomainError.Unauthorized(
                "Auth.Unauthenticated",
                "Authentication is required."));
        }

        var client = await clientRepository.GetByIdAsync(currentUser.ClientId.Value, cancellationToken);
        if (client is null)
        {
            return Result.Failure<ClientResponse>(DomainError.NotFound(
                "Clients.NotFound",
                "Client was not found."));
        }

        return Result.Success(MapToResponse(client));
    }

    public async Task<Result<IReadOnlyList<ClientResponse>>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var clients = await clientRepository.ListAllAsync(cancellationToken);
        IReadOnlyList<ClientResponse> response = clients.Select(MapToResponse).ToList();
        return Result.Success(response);
    }

    public async Task<Result<ClientResponse>> UpdateStatusAsync(
        Guid clientId,
        UpdateClientStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ClientStatus? parsedStatus = request.Status switch
        {
            nameof(ClientStatus.Active) => ClientStatus.Active,
            nameof(ClientStatus.Blocked) => ClientStatus.Blocked,
            _ => null
        };

        if (parsedStatus is not { } newStatus)
        {
            return Result.Failure<ClientResponse>(DomainError.Validation(
                "Clients.InvalidStatus",
                "Status must be Active or Blocked."));
        }

        var client = await clientRepository.GetByIdAsync(clientId, cancellationToken);
        if (client is null)
        {
            return Result.Failure<ClientResponse>(DomainError.NotFound(
                "Clients.NotFound",
                "Client was not found."));
        }

        var updateResult = client.UpdateStatus(newStatus, request.Reason);
        if (updateResult.IsFailure)
        {
            return Result.Failure<ClientResponse>(updateResult.Error);
        }

        await clientRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(MapToResponse(client));
    }

    private static ClientResponse MapToResponse(Client client) =>
        new(
            client.Id,
            client.FullName,
            client.Email,
            client.Phone,
            client.BirthDate,
            client.TaxId,
            client.Role.ToString(),
            client.Status.ToString(),
            client.CreatedAtUtc);
}
