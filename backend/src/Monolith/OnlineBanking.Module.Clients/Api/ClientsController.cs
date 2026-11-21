using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineBanking.Infrastructure.Http;
using OnlineBanking.Module.Clients.Application.Dtos;
using OnlineBanking.Module.Clients.Application.Services;
using OnlineBanking.Module.Clients.Domain;

namespace OnlineBanking.Module.Clients.Api;

[ApiController]
[Authorize]
[Route("api/v1/clients")]
public sealed class ClientsController(ClientsService clientsService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var result = await clientsService.GetMeAsync(cancellationToken);
        return result.ToActionResult(HttpContext);
    }

    [Authorize(Roles = nameof(ClientRole.Admin))]
    [HttpGet]
    public async Task<IActionResult> ListAll(CancellationToken cancellationToken)
    {
        var result = await clientsService.ListAllAsync(cancellationToken);
        return result.ToActionResult(HttpContext);
    }

    [Authorize(Roles = nameof(ClientRole.Admin))]
    [HttpPatch("{clientId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid clientId,
        [FromBody] UpdateClientStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await clientsService.UpdateStatusAsync(clientId, request, cancellationToken);
        return result.ToActionResult(HttpContext);
    }
}
