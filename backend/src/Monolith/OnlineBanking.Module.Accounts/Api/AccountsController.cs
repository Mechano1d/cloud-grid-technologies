using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineBanking.Infrastructure.Http;
using OnlineBanking.Module.Accounts.Application.Dtos;
using OnlineBanking.Module.Accounts.Application.Services;

namespace OnlineBanking.Module.Accounts.Api;

[ApiController]
[Authorize]
[Route("api/v1/accounts")]
public sealed class AccountsController(AccountsService accountsService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListMyAccounts(CancellationToken cancellationToken)
    {
        var result = await accountsService.ListMyAccountsAsync(cancellationToken);
        return result.ToActionResult(HttpContext);
    }

    [HttpPost]
    public async Task<IActionResult> OpenAccount(
        [FromBody] OpenAccountRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await accountsService.OpenAccountAsync(request ?? new OpenAccountRequest(), cancellationToken);
        return result.IsSuccess
            ? result.ToCreatedResult(HttpContext, $"/api/v1/accounts/{result.Value.Id}")
            : result.ToActionResult(HttpContext);
    }

    [HttpGet("{accountId:guid}")]
    public async Task<IActionResult> GetById(Guid accountId, CancellationToken cancellationToken)
    {
        var result = await accountsService.GetByIdAsync(accountId, cancellationToken);
        return result.ToActionResult(HttpContext);
    }

    [HttpPost("{accountId:guid}/close")]
    public async Task<IActionResult> Close(Guid accountId, CancellationToken cancellationToken)
    {
        var result = await accountsService.CloseAccountAsync(accountId, cancellationToken);
        return result.ToActionResult(HttpContext);
    }
}
