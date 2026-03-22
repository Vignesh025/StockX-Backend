using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockX.Core.Services.Interfaces;
using System.Security.Claims;

namespace StockX.API.Controllers;

[ApiController]
[Authorize]
[Route("api/wallet")]
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
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var wallet = await _walletService.GetWalletBalanceAsync(userId, cancellationToken);

        return Ok(new
        {
            balance = wallet.Balance,
            lastUpdated = wallet.LastUpdated
        });
    }

    [HttpPost("deposit/initiate")]
    public async Task<ActionResult<object>> InitiateDeposit(
        [FromBody] DepositRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.InitiateDepositAsync(
            userId,
            request.Amount,
            cancellationToken);

        return Ok(new
        {
            checkoutUrl = result.CheckoutUrl,
            paymentIntentId = result.PaymentIntentId
        });
    }

    public sealed record DepositRequest(decimal Amount);
}

