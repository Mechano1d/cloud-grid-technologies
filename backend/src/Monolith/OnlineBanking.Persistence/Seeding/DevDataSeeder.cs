using Microsoft.EntityFrameworkCore;
using OnlineBanking.Module.Accounts.Domain;
using OnlineBanking.Module.Clients.Domain;
using OnlineBanking.Shared.Time;

namespace OnlineBanking.Persistence.Seeding;

public static class DevDataSeeder
{
    public static readonly Guid AdminClientId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    public static readonly Guid AliceClientId = Guid.Parse("11111111-1111-1111-1111-111111111102");
    public static readonly Guid BobClientId = Guid.Parse("11111111-1111-1111-1111-111111111103");

    public static readonly Guid AliceAccountId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    public static readonly Guid BobAccountId = Guid.Parse("22222222-2222-2222-2222-222222222202");

    public static async Task SeedAsync(
        OnlineBankingDbContext dbContext,
        Func<string, string> hashPassword,
        IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        await EnsureClientAsync(
            dbContext,
            AdminClientId,
            "System Administrator",
            "admin@onlinebanking.local",
            "+380670000001",
            new DateOnly(1990, 1, 1),
            "1000000001",
            "Admin#2026!",
            hashPassword,
            ClientRole.Admin,
            now,
            cancellationToken);

        await EnsureClientAsync(
            dbContext,
            AliceClientId,
            "Alice Kovalenko",
            "alice@onlinebanking.local",
            "+380671110001",
            new DateOnly(1998, 4, 12),
            "3000000001",
            "Client#2026!",
            hashPassword,
            ClientRole.Client,
            now,
            cancellationToken);

        await EnsureClientAsync(
            dbContext,
            BobClientId,
            "Bob Shevchenko",
            "bob@onlinebanking.local",
            "+380671110002",
            new DateOnly(1997, 8, 25),
            "3000000002",
            "Client#2026!",
            hashPassword,
            ClientRole.Client,
            now,
            cancellationToken);

        await EnsureAccountAsync(
            dbContext,
            AliceAccountId,
            AliceClientId,
            IbanGenerator.CreateFromBban(IbanGenerator.DefaultBankCode, "0000026007233566001"),
            Currency.UAH,
            10000.00m,
            now,
            cancellationToken);

        await EnsureAccountAsync(
            dbContext,
            BobAccountId,
            BobClientId,
            IbanGenerator.CreateFromBban(IbanGenerator.DefaultBankCode, "0000026007233566002"),
            Currency.UAH,
            5000.00m,
            now,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureClientAsync(
        OnlineBankingDbContext dbContext,
        Guid id,
        string fullName,
        string email,
        string phone,
        DateOnly birthDate,
        string taxId,
        string rawPassword,
        Func<string, string> hashPassword,
        ClientRole role,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Clients.AnyAsync(
            c => c.Id == id || c.Email == email || c.TaxId == taxId,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var client = Client.Create(
            fullName,
            email,
            phone,
            birthDate,
            taxId,
            hashPassword(rawPassword),
            createdAtUtc,
            role,
            id).Value;

        await dbContext.Clients.AddAsync(client, cancellationToken);
    }

    private static async Task EnsureAccountAsync(
        OnlineBankingDbContext dbContext,
        Guid id,
        Guid clientId,
        string iban,
        Currency currency,
        decimal initialDeposit,
        DateTimeOffset openedAtUtc,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Accounts.AnyAsync(
            a => a.Id == id || a.Iban == iban,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var account = Account.Open(clientId, iban, currency, initialDeposit, openedAtUtc, id).Value;
        await dbContext.Accounts.AddAsync(account, cancellationToken);
    }
}
