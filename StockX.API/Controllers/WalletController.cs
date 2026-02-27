using Microsoft.AspNetCore.Mvc;
using StockX.Core.Services.Interfaces;

namespace StockX.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly IPaymentService _paymentService;

    public WalletController(
        IWalletService walletService,
        IPaymentService paymentService)
    {
        _walletService = walletService;
        _paymentService = paymentService;
    }

    [HttpGet("balance")]
    public async Task<ActionResult<object>> GetBalance(
        CancellationToken cancellationToken)
    {
        // Placeholder user id for now.
        return Unauthorized();
    }

    [HttpPost("deposit/initiate")]
    public async Task<ActionResult<object>> InitiateDeposit(
        [FromBody] DepositRequest request,
        CancellationToken cancellationToken)
    {
        // Placeholder user id for now.
        return Unauthorized();
    }

    public sealed record DepositRequest(decimal Amount);
}

