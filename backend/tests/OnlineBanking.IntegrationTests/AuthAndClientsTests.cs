using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OnlineBanking.Infrastructure.Auth;
using OnlineBanking.IntegrationTests.Infrastructure;
using OnlineBanking.Persistence;
using OnlineBanking.Persistence.Seeding;
using OnlineBanking.Shared.Time;

namespace OnlineBanking.IntegrationTests;

[Collection(BankingApiSuite.Name)]
public sealed class AuthAndClientsTests(BankingApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyWithoutAuth()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmailOrTaxId_ReturnsConflict()
    {
        using var client = factory.CreateClient();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var payload = new
        {
            fullName = "Olena Koval",
            email = $"olena-{tag}@example.com",
            phone = "+380671234567",
            birthDate = "1999-03-10",
            taxId = factory.NextTaxId(),
            password = "Password#2026!"
        };

        var first = await client.PostAsJsonAsync(new Uri("/api/v1/auth/register", UriKind.Relative), payload);
        var duplicate = await client.PostAsJsonAsync(new Uri("/api/v1/auth/register", UriKind.Relative), payload);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task ClientsEndpoints_EnforceAuthenticationAndAdminRole()
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var userAuth = await factory.RegisterClientAsync(tag);
        var adminAuth = await factory.CreateAdminAsync(Guid.NewGuid().ToString("N")[..10]);

        using var anonymous = factory.CreateClient();
        using var userClient = factory.CreateAuthorizedClient(userAuth.AccessToken);
        using var adminClient = factory.CreateAuthorizedClient(adminAuth.AccessToken);

        var unauthMe = await anonymous.GetAsync(new Uri("/api/v1/clients/me", UriKind.Relative));
        var meResponse = await userClient.GetAsync(new Uri("/api/v1/clients/me", UriKind.Relative));
        var userListClients = await userClient.GetAsync(new Uri("/api/v1/clients", UriKind.Relative));
        var adminListClients = await adminClient.GetAsync(new Uri("/api/v1/clients", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, unauthMe.StatusCode);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, userListClients.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminListClients.StatusCode);
    }

    [Fact]
    public async Task BlockClient_PreventsLoginAndRejectsMutationsWithAlreadyIssuedJwt()
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var userAuth = await factory.RegisterClientAsync(tag);
        var recipientAuth = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);
        var adminAuth = await factory.CreateAdminAsync(Guid.NewGuid().ToString("N")[..10]);

        using var userClient = factory.CreateAuthorizedClient(userAuth.AccessToken);
        using var recipientClient = factory.CreateAuthorizedClient(recipientAuth.AccessToken);
        using var adminClient = factory.CreateAuthorizedClient(adminAuth.AccessToken);

        var userFunded = await (await userClient.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 500.00m }))
            .Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        var userEmpty = await (await userClient.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 0.00m }))
            .Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        var recipientAccount = await (await recipientClient.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 0.00m }))
            .Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        Assert.NotNull(userFunded);
        Assert.NotNull(userEmpty);
        Assert.NotNull(recipientAccount);

        var blockResponse = await adminClient.PatchAsJsonAsync(
            new Uri($"/api/v1/clients/{userAuth.ClientId}/status", UriKind.Relative),
            new { status = "Blocked", reason = "Compliance hold" });

        Assert.Equal(HttpStatusCode.OK, blockResponse.StatusCode);
        var updated = await blockResponse.Content.ReadFromJsonAsync<ClientPayload>(BankingApiFactory.JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Blocked", updated.Status);

        using var anon = factory.CreateClient();
        var loginResponse = await anon.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new { email = $"user-{tag}@example.com", password = "Password#2026!" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);

        var openBlocked = await userClient.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 0.00m });
        var closeBlocked = await userClient.PostAsync(
            new Uri($"/api/v1/accounts/{userEmpty.Id}/close", UriKind.Relative),
            null);
        var transferBlocked = await userClient.PostAsJsonAsync(
            new Uri("/api/v1/transactions/transfers", UriKind.Relative),
            new
            {
                fromAccountId = userFunded.Id,
                toIban = recipientAccount.Iban,
                amount = 100.00m,
                description = "Blocked attempt"
            });

        Assert.Equal(HttpStatusCode.Forbidden, openBlocked.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, closeBlocked.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, transferBlocked.StatusCode);
    }

    [Fact]
    public async Task BlockSelf_AsAdmin_ReturnsConflict()
    {
        var adminAuth = await factory.CreateAdminAsync(Guid.NewGuid().ToString("N")[..10]);
        using var adminClient = factory.CreateAuthorizedClient(adminAuth.AccessToken);

        var response = await adminClient.PatchAsJsonAsync(
            new Uri($"/api/v1/clients/{adminAuth.ClientId}/status", UriKind.Relative),
            new { status = "Blocked", reason = "Self block test" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AdminBootstrapper_CreatesConfiguredAdminOnce_AndSkipsWhenUnconfigured()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OnlineBankingDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
            var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            var skipped = await AdminBootstrapper.BootstrapAsync(
                dbContext,
                new AdminBootstrapOptions(),
                passwordHasher.Hash,
                dateTimeProvider);
            Assert.False(skipped);

            var options = new AdminBootstrapOptions
            {
                Email = "ops-admin@example.com",
                Password = "StrongAdminPassword#2026",
                TaxId = factory.NextTaxId()
            };

            var created = await AdminBootstrapper.BootstrapAsync(
                dbContext,
                options,
                passwordHasher.Hash,
                dateTimeProvider);
            var duplicate = await AdminBootstrapper.BootstrapAsync(
                dbContext,
                options,
                passwordHasher.Hash,
                dateTimeProvider);

            Assert.True(created);
            Assert.False(duplicate);
        }

        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new { email = "ops-admin@example.com", password = "StrongAdminPassword#2026" });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthPayload>(BankingApiFactory.JsonOptions);
        Assert.NotNull(auth);
        Assert.Equal("Admin", auth.Role);
    }
}
