using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OnlineBanking.Infrastructure.Auth;
using OnlineBanking.Module.Clients.Domain;
using OnlineBanking.Persistence;
using OnlineBanking.Shared.Time;
using Respawn;
using Testcontainers.PostgreSql;

namespace OnlineBanking.IntegrationTests.Infrastructure;

public sealed class BankingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private Respawner? _respawner;
    private long _taxIdSequence = 4_000_000_000;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string NextTaxId() =>
        Interlocked.Increment(ref _taxIdSequence).ToString(CultureInfo.InvariantCulture);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        using var _ = CreateClient();

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["clients", "accounts", "transactions"]
        });
    }

    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    public HttpClient CreateAuthorizedClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public async Task<AuthPayload> CreateAdminAsync(string uniqueTag)
    {
        var email = $"admin-{uniqueTag}@example.com";
        const string password = "AdminPass#2026!";

        await using (var scope = Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OnlineBankingDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
            var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            var admin = Client.Create(
                $"Admin {uniqueTag}",
                email,
                "+380670000001",
                new DateOnly(1990, 1, 1),
                NextTaxId(),
                passwordHasher.Hash(password),
                dateTimeProvider.UtcNow,
                ClientRole.Admin).Value;

            await dbContext.Clients.AddAsync(admin);
            await dbContext.SaveChangesAsync();
        }

        using var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthPayload>(JsonOptions))!;
    }

    public async Task<AuthPayload> RegisterClientAsync(string uniqueTag)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/register", UriKind.Relative),
            new
            {
                fullName = $"Test User {uniqueTag}",
                email = $"user-{uniqueTag}@example.com",
                phone = "+380671234567",
                birthDate = "1998-05-15",
                taxId = NextTaxId(),
                password = "Password#2026!"
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthPayload>(JsonOptions))!;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Issuer", "OnlineBanking.Api");
        builder.UseSetting("Jwt:Audience", "OnlineBanking.Web");
        builder.UseSetting("Jwt:SecretKey", "integration-test-jwt-secret-key-min-32-chars");
        builder.UseSetting("Jwt:ExpirationMinutes", "60");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Database:SeedOnStartup", "false");
    }
}

[CollectionDefinition(Name)]
public sealed class BankingApiSuite : ICollectionFixture<BankingApiFactory>
{
    public const string Name = "BankingApi";
}

public sealed record AuthPayload(string AccessToken, int ExpiresInSeconds, Guid ClientId, string Role);

public sealed record ClientPayload(
    Guid Id,
    string FullName,
    string Email,
    string Phone,
    string BirthDate,
    string TaxId,
    string Role,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record AccountPayload(
    Guid Id,
    Guid ClientId,
    string Iban,
    string Currency,
    decimal Balance,
    decimal BlockedAmount,
    decimal AvailableBalance,
    string Status,
    DateTimeOffset OpenedAtUtc);

public sealed record TransactionPayload(
    Guid Id,
    Guid FromAccountId,
    Guid ToAccountId,
    string ToIban,
    decimal Amount,
    string Currency,
    string Status,
    string Type,
    string? Description,
    string? IdempotencyKey,
    DateTimeOffset CreatedAtUtc);

public sealed record TransactionPagePayload(
    IReadOnlyList<TransactionPayload> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ProblemPayload(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Code,
    string? TraceId);
