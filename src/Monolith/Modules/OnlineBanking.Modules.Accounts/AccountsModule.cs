using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineBanking.BuildingBlocks.Modules;

namespace OnlineBanking.Modules.Accounts;

public sealed class AccountsModule : IModule
{
    public string Name => "Accounts";

    public IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}
