using Microsoft.EntityFrameworkCore;
using OnlineBanking.Module.Accounts.Domain;
using OnlineBanking.Module.Clients.Domain;
using OnlineBanking.Module.Transactions.Domain;

namespace OnlineBanking.Persistence;

public sealed class OnlineBankingDbContext(DbContextOptions<OnlineBankingDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OnlineBankingDbContext).Assembly);
    }
}
