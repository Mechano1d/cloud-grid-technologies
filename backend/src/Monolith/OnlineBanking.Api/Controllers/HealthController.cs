using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineBanking.Persistence;
using OnlineBanking.Shared.Time;

namespace OnlineBanking.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("health")]
public sealed class HealthController(
    OnlineBankingDbContext dbContext,
    IDateTimeProvider dateTimeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        var payload = new
        {
            status = canConnect ? "Healthy" : "Unhealthy",
            service = "online-banking-monolith",
            timestampUtc = dateTimeProvider.UtcNow
        };

        return canConnect
            ? Ok(payload)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, payload);
    }
}
