using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineBanking.BuildingBlocks.Modules;

namespace OnlineBanking.Modules.Transactions;

public sealed class TransactionsModule : IModule
{
    public string Name => "Transactions";

    public IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}
