using Microsoft.Extensions.DependencyInjection;
using OnlineBanking.Module.Clients.Application;
using OnlineBanking.Module.Clients.Application.Services;
using OnlineBanking.Shared.Contracts;

namespace OnlineBanking.Module.Clients;

public static class ClientsModule
{
    public static IServiceCollection AddClientsModule(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<ClientsService>();
        services.AddScoped<IClientsApi, ClientsApiAdapter>();
        return services;
    }
}
