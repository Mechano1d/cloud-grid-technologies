using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OnlineBanking.Persistence;

internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OnlineBankingDbContext>
{
    public OnlineBankingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=online_banking;Username=banking_user";

        var optionsBuilder = new DbContextOptionsBuilder<OnlineBankingDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"));

        return new OnlineBankingDbContext(optionsBuilder.Options);
    }
}
