using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineBanking.BuildingBlocks.Modules;

namespace OnlineBanking.Modules.Clients;

public sealed class ClientsModule : IModule
{
    public string Name => "Clients";

    public IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}
