using OnlineBanking.Module.Clients.Application.Abstractions;
using OnlineBanking.Module.Clients.Domain;
using OnlineBanking.Shared.Contracts;

namespace OnlineBanking.Module.Clients.Application;

internal sealed class ClientsApiAdapter(IClientRepository clientRepository) : IClientsApi
{
    public async Task<ClientSummaryDto?> GetByIdAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetByIdAsync(clientId, cancellationToken);
        return client is null
            ? null
            : new ClientSummaryDto(
                client.Id,
                client.FullName,
                client.Email,
                client.Role.ToString(),
                client.Status.ToString());
    }

    public async Task<bool> IsActiveClientAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetByIdAsync(clientId, cancellationToken);
        return client is not null && client.Status == ClientStatus.Active;
    }
}
