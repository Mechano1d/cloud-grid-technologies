using OnlineBanking.Module.Clients.Application.Abstractions;
using OnlineBanking.Shared.Contracts;

namespace OnlineBanking.Module.Clients.Application;

internal sealed class ClientsApiAdapter(IClientRepository clientRepository) : IClientsApi
{
    public Task<bool> IsActiveClientAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        clientRepository.IsActiveAsync(clientId, cancellationToken);
}
