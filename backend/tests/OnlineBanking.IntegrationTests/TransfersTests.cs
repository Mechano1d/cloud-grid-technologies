using System.Net;
using System.Net.Http.Json;
using OnlineBanking.IntegrationTests.Infrastructure;

namespace OnlineBanking.IntegrationTests;

[Collection(BankingApiSuite.Name)]
public sealed class TransfersTests(BankingApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateTransfer_UpdatesBalancesAtomically_AndRespectsIdempotencyKey()
    {
        var senderAuth = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);
        var recipientAuth = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);

        using var senderClient = factory.CreateAuthorizedClient(senderAuth.AccessToken);
        using var recipientClient = factory.CreateAuthorizedClient(recipientAuth.AccessToken);

        var fromAcc = await (await senderClient.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 1000.00m }))
            .Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        var toAcc = await (await recipientClient.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 100.00m }))
            .Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        Assert.NotNull(fromAcc);
        Assert.NotNull(toAcc);

        var idempotencyKey = $"idem-{Guid.NewGuid():N}";
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transactions/transfers")
        {
            Content = JsonContent.Create(new
            {
                fromAccountId = fromAcc.Id,
                toIban = toAcc.Iban,
                amount = 350.00m,
                description = "Invoice #42"
            })
        };
        firstRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var firstResponse = await senderClient.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstTx = await firstResponse.Content.ReadFromJsonAsync<TransactionPayload>(BankingApiFactory.JsonOptions);
        Assert.NotNull(firstTx);
        Assert.Equal("Completed", firstTx.Status);

        using var retryRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transactions/transfers")
        {
            Content = JsonContent.Create(new
            {
                fromAccountId = fromAcc.Id,
                toIban = toAcc.Iban,
                amount = 350.00m,
                description = "Invoice #42"
            })
        };
        retryRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var retryResponse = await senderClient.SendAsync(retryRequest);
        Assert.Equal(HttpStatusCode.Created, retryResponse.StatusCode);
        var retryTx = await retryResponse.Content.ReadFromJsonAsync<TransactionPayload>(BankingApiFactory.JsonOptions);
        Assert.NotNull(retryTx);
        Assert.Equal(firstTx.Id, retryTx.Id);

        using var mismatchedDescriptionRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transactions/transfers")
        {
            Content = JsonContent.Create(new
            {
                fromAccountId = fromAcc.Id,
                toIban = toAcc.Iban,
                amount = 350.00m,
                description = "Altered description"
            })
        };
        mismatchedDescriptionRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var mismatchResponse = await senderClient.SendAsync(mismatchedDescriptionRequest);
        Assert.Equal(HttpStatusCode.Conflict, mismatchResponse.StatusCode);
        var mismatchProblem = await mismatchResponse.Content.ReadFromJsonAsync<ProblemPayload>(BankingApiFactory.JsonOptions);
        Assert.Equal("Transactions.IdempotencyKeyMismatch", mismatchProblem?.Code);

        var updatedFrom = await senderClient.GetFromJsonAsync<AccountPayload>(
            new Uri($"/api/v1/accounts/{fromAcc.Id}", UriKind.Relative),
            BankingApiFactory.JsonOptions);
        var updatedTo = await recipientClient.GetFromJsonAsync<AccountPayload>(
            new Uri($"/api/v1/accounts/{toAcc.Id}", UriKind.Relative),
            BankingApiFactory.JsonOptions);

        Assert.NotNull(updatedFrom);
        Assert.NotNull(updatedTo);
        Assert.Equal(650.00m, updatedFrom.Balance);
        Assert.Equal(450.00m, updatedTo.Balance);
    }

    [Fact]
    public async Task CreateTransfer_WithInsufficientFunds_PersistsFailedAndReplaysOriginalError()
    {
        var senderAuth = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);
        var recipientAuth = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);
        var thirdAuth = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);

        using var senderClient = factory.CreateAuthorizedClient(senderAuth.AccessToken);
        using var recipientClient = factory.CreateAuthorizedClient(recipientAuth.AccessToken);
        using var thirdClient = factory.CreateAuthorizedClient(thirdAuth.AccessToken);

        var fromAcc = await (await senderClient.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 50.00m }))
            .Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        var toAcc = await (await recipientClient.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 0.00m }))
            .Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        Assert.NotNull(fromAcc);
        Assert.NotNull(toAcc);

        var failedKey = $"fail-{Guid.NewGuid():N}";
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transactions/transfers")
        {
            Content = JsonContent.Create(new
            {
                fromAccountId = fromAcc.Id,
                toIban = toAcc.Iban,
                amount = 200.00m,
                description = "Too large"
            })
        };
        firstRequest.Headers.Add("Idempotency-Key", failedKey);

        var firstResponse = await senderClient.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.Conflict, firstResponse.StatusCode);
        var firstProblem = await firstResponse.Content.ReadFromJsonAsync<ProblemPayload>(BankingApiFactory.JsonOptions);
        Assert.Equal("Accounts.InsufficientFunds", firstProblem?.Code);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transactions/transfers")
        {
            Content = JsonContent.Create(new
            {
                fromAccountId = fromAcc.Id,
                toIban = toAcc.Iban,
                amount = 200.00m,
                description = "Too large"
            })
        };
        replayRequest.Headers.Add("Idempotency-Key", failedKey);

        var replayResponse = await senderClient.SendAsync(replayRequest);
        Assert.Equal(HttpStatusCode.Conflict, replayResponse.StatusCode);
        var replayProblem = await replayResponse.Content.ReadFromJsonAsync<ProblemPayload>(BankingApiFactory.JsonOptions);
        Assert.Equal("Accounts.InsufficientFunds", replayProblem?.Code);

        var history = await senderClient.GetFromJsonAsync<TransactionPagePayload>(
            new Uri($"/api/v1/transactions?accountId={fromAcc.Id}&page=1&pageSize=10", UriKind.Relative),
            BankingApiFactory.JsonOptions);

        Assert.NotNull(history);
        Assert.Equal(1, history.TotalCount);
        var failedTx = Assert.Single(history.Items);
        Assert.Equal("Failed", failedTx.Status);

        var thirdPartyGet = await thirdClient.GetAsync(
            new Uri($"/api/v1/transactions/{failedTx.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Forbidden, thirdPartyGet.StatusCode);

        var senderBalanceAfter = await senderClient.GetFromJsonAsync<AccountPayload>(
            new Uri($"/api/v1/accounts/{fromAcc.Id}", UriKind.Relative),
            BankingApiFactory.JsonOptions);
        Assert.NotNull(senderBalanceAfter);
        Assert.Equal(50.00m, senderBalanceAfter.Balance);
    }

    [Fact]
    public async Task CreateTransfer_CurrencyMismatchOrClosedAccount_ReturnsConflictAndMarksFailed()
    {
        var senderAuth = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);
        var recipientAuth = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);

        using var senderClient = factory.CreateAuthorizedClient(senderAuth.AccessToken);
        using var recipientClient = factory.CreateAuthorizedClient(recipientAuth.AccessToken);

        var uahSource = await (await senderClient.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 500.00m }))
            .Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        var usdDest = await (await recipientClient.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "USD", initialDeposit = 0.00m }))
            .Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        var closedUahDest = await (await recipientClient.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 0.00m }))
            .Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        Assert.NotNull(uahSource);
        Assert.NotNull(usdDest);
        Assert.NotNull(closedUahDest);

        var closeRes = await recipientClient.PostAsync(
            new Uri($"/api/v1/accounts/{closedUahDest.Id}/close", UriKind.Relative),
            null);
        Assert.Equal(HttpStatusCode.OK, closeRes.StatusCode);

        var currencyMismatchRes = await senderClient.PostAsJsonAsync(
            new Uri("/api/v1/transactions/transfers", UriKind.Relative),
            new { fromAccountId = uahSource.Id, toIban = usdDest.Iban, amount = 50.00m });
        Assert.Equal(HttpStatusCode.Conflict, currencyMismatchRes.StatusCode);
        var mismatchProblem = await currencyMismatchRes.Content.ReadFromJsonAsync<ProblemPayload>(BankingApiFactory.JsonOptions);
        Assert.Equal("Accounts.CurrencyMismatch", mismatchProblem?.Code);

        var closedDestRes = await senderClient.PostAsJsonAsync(
            new Uri("/api/v1/transactions/transfers", UriKind.Relative),
            new { fromAccountId = uahSource.Id, toIban = closedUahDest.Iban, amount = 50.00m });
        Assert.Equal(HttpStatusCode.Conflict, closedDestRes.StatusCode);
        var closedProblem = await closedDestRes.Content.ReadFromJsonAsync<ProblemPayload>(BankingApiFactory.JsonOptions);
        Assert.Equal("Accounts.NotActive", closedProblem?.Code);
    }

    [Fact]
    public async Task ConcurrentOppositeTransfers_CompleteWithoutDeadlock_AndPreserveTotalBalance()
    {
        var authA = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);
        var authB = await factory.RegisterClientAsync(Guid.NewGuid().ToString("N")[..10]);

        using var clientA = factory.CreateAuthorizedClient(authA.AccessToken);
        using var clientB = factory.CreateAuthorizedClient(authB.AccessToken);

        var accA = await (await clientA.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 1000.00m }))
            .Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        var accB = await (await clientB.PostAsJsonAsync(
            new Uri("/api/v1/accounts", UriKind.Relative),
            new { currency = "UAH", initialDeposit = 1000.00m }))
            .Content.ReadFromJsonAsync<AccountPayload>(BankingApiFactory.JsonOptions);

        Assert.NotNull(accA);
        Assert.NotNull(accB);

        var tasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(clientA.PostAsJsonAsync(
                new Uri("/api/v1/transactions/transfers", UriKind.Relative),
                new { fromAccountId = accA.Id, toIban = accB.Iban, amount = 50.00m, description = $"A2B-{i}" }));
            tasks.Add(clientB.PostAsJsonAsync(
                new Uri("/api/v1/transactions/transfers", UriKind.Relative),
                new { fromAccountId = accB.Id, toIban = accA.Iban, amount = 30.00m, description = $"B2A-{i}" }));
        }

        var responses = await Task.WhenAll(tasks);
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            response.Dispose();
        }

        var finalA = await clientA.GetFromJsonAsync<AccountPayload>(
            new Uri($"/api/v1/accounts/{accA.Id}", UriKind.Relative),
            BankingApiFactory.JsonOptions);
        var finalB = await clientB.GetFromJsonAsync<AccountPayload>(
            new Uri($"/api/v1/accounts/{accB.Id}", UriKind.Relative),
            BankingApiFactory.JsonOptions);

        Assert.NotNull(finalA);
        Assert.NotNull(finalB);
        Assert.Equal(900.00m, finalA.Balance);
        Assert.Equal(1100.00m, finalB.Balance);
        Assert.Equal(2000.00m, finalA.Balance + finalB.Balance);
    }
}
