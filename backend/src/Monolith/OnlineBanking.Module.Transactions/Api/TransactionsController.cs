using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineBanking.Infrastructure.Http;
using OnlineBanking.Module.Transactions.Application.Dtos;
using OnlineBanking.Module.Transactions.Application.Services;

namespace OnlineBanking.Module.Transactions.Api;

[ApiController]
[Authorize]
[Route("api/v1/transactions")]
public sealed class TransactionsController(TransfersService transfersService) : ControllerBase
{
    [HttpPost("transfers")]
    public async Task<IActionResult> CreateTransfer(
        [FromBody] CreateTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await transfersService.CreateTransferAsync(request, idempotencyKey, cancellationToken);
        return result.ToCreatedResult(HttpContext, tx => $"/api/v1/transactions/{tx.Id}");
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? accountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await transfersService.ListAsync(accountId, page, pageSize, cancellationToken);
        return result.ToActionResult(HttpContext);
    }

    [HttpGet("{transactionId:guid}")]
    public async Task<IActionResult> GetById(Guid transactionId, CancellationToken cancellationToken)
    {
        var result = await transfersService.GetByIdAsync(transactionId, cancellationToken);
        return result.ToActionResult(HttpContext);
    }
}
