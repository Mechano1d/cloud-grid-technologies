using Microsoft.EntityFrameworkCore;
using OnlineBanking.Module.Clients.Domain;
using OnlineBanking.Shared.Time;

namespace OnlineBanking.Persistence.Seeding;

public static class AdminBootstrapper
{
    public static async Task<bool> BootstrapAsync(
        OnlineBankingDbContext dbContext,
        AdminBootstrapOptions options,
        Func<string, string> hashPassword,
        IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            return false;
        }

        if (options.Password.Length < 8)
        {
            throw new InvalidOperationException("AdminBootstrap:Password must be at least 8 characters long.");
        }

        var normalizedEmail = options.Email.Trim().ToLowerInvariant();
        var taxId = string.IsNullOrWhiteSpace(options.TaxId) ? "1000000000" : options.TaxId.Trim();

        var exists = await dbContext.Clients.AnyAsync(
            c => c.Email == normalizedEmail || c.TaxId == taxId,
            cancellationToken);

        if (exists)
        {
            return false;
        }

        var fullName = string.IsNullOrWhiteSpace(options.FullName) ? "System Administrator" : options.FullName.Trim();
        var phone = string.IsNullOrWhiteSpace(options.Phone) ? "+380670000000" : options.Phone.Trim();
        var birthDate = options.BirthDate ?? new DateOnly(1990, 1, 1);

        var result = Client.Create(
            fullName,
            normalizedEmail,
            phone,
            birthDate,
            taxId,
            hashPassword(options.Password),
            dateTimeProvider.UtcNow,
            ClientRole.Admin);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Invalid AdminBootstrap configuration: {result.Error.Message}");
        }

        await dbContext.Clients.AddAsync(result.Value, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
