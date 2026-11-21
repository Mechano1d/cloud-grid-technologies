using System.Net;
using System.Net.Http.Json;
using OnlineBanking.IntegrationTests.Infrastructure;

namespace OnlineBanking.IntegrationTests;

[Collection(BankingApiSuite.Name)]
public sealed class AccountsTests(BankingApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OpenAndListAccounts_EnforcesOwnershipAcrossClients()
    {
        var ownerAuth = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);
        var otherAuth = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);

        using var ownerClient = factory.CreateAuthorizedClient(ownerAuth.AccessToken);
        using var otherClient = factory.CreateAuthorizedClient(otherAuth.AccessToken);

        var openResponse = await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 250.00m });

        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);
        var opened = await openResponse.Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);
        Assert.NotNull(opened);
        Assert.Equal(250.00m, opened.Balance);
        Assert.Equal(29, opened.Iban.Length);

        var listResponse = await ownerClient.GetFromJsonAsync<List<AccountPayload>>(
            new Uri("/api/v1/accounts", UriKind.Relative),
            BankingApiFactory.JsonOptions);
        Assert.NotNull(listResponse);
        Assert.Contains(listResponse, a => a.Id == opened.Id);

        var foreignGet = await otherClient.GetAsync(new Uri($"/api/v1/accounts/{opened.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Forbidden, foreignGet.StatusCode);
    }

    [Fact]
    public async Task CloseAccount_FailsWithNonZeroBalance_AndSucceedsWithZeroBalance()
    {
        var auth = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);
        using var client = factory.CreateAuthorizedClient(auth.AccessToken);

        var fundedRes = await client.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 100.00m });
        var funded = await fundedRes.Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        var emptyRes = await client.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "USD", initialDeposit = 0.00m });
        var empty = await emptyRes.Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        Assert.NotNull(funded);
        Assert.NotNull(empty);

        var closeFunded = await client.PostAsync(new Uri($"/api/v1/accounts/{funded.Id}/close", UriKind.Relative), null);
        var closeEmpty = await client.PostAsync(new Uri($"/api/v1/accounts/{empty.Id}/close", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.Conflict, closeFunded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, closeEmpty.StatusCode);

        var closed = await closeEmpty.Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);
        Assert.NotNull(closed);
        Assert.Equal("Closed", closed.Status);
    }
}
