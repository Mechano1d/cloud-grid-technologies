using Microsoft.Extensions.DependencyInjection;
using OnlineBanking.Module.Accounts.Application;
using OnlineBanking.Module.Accounts.Application.Services;
using OnlineBanking.Shared.Contracts;

namespace OnlineBanking.Module.Accounts;

public static class AccountsModule
{
    public static IServiceCollection AddAccountsModule(this IServiceCollection services)
    {
        services.AddScoped<AccountsService>();
        services.AddScoped<IAccountsApi, AccountsApiAdapter>();
        return services;
    }
}
