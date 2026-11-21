using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineBanking.Shared.Time;

namespace OnlineBanking.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("health")]
public sealed class HealthController(IDateTimeProvider dateTimeProvider) : ControllerBase
{
    [HttpGet]
    public IActionResult GetHealth() =>
        Ok(new
        {
            status = "Healthy",
            service = "online-banking-monolith",
            timestampUtc = dateTimeProvider.UtcNow
        });
}
