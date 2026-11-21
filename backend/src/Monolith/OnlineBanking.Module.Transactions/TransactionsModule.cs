using Microsoft.Extensions.DependencyInjection;
using OnlineBanking.Module.Transactions.Application.Services;

namespace OnlineBanking.Module.Transactions;

public static class TransactionsModule
{
    public static IServiceCollection AddTransactionsModule(this IServiceCollection services)
    {
        services.AddScoped<TransfersService>();
        return services;
    }
}
