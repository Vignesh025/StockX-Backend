using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using StockX.Core.DTOs.Trading;
using StockX.Core.DTOs.Wallet;
using StockX.Core.Services.Interfaces;

namespace StockX.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TradingController : ControllerBase
{
    private readonly ITradingService _tradingService;

    public TradingController(ITradingService tradingService)
    {
        _tradingService = tradingService;
    }

    [HttpPost("buy")]
    public async Task<ActionResult<TradeResponse>> Buy(
        [FromBody] BuyStockRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await _tradingService.BuyAsync(
            userId,
            request.Symbol,
            request.Quantity,
            cancellationToken);

        return Ok(new TradeResponse(
            result.Success,
            new TransactionDto(
                result.Transaction.TransactionId,
                result.Transaction.Type,
                result.Transaction.Amount,
                result.Transaction.StockSymbol,
                result.Transaction.Quantity,
                result.Transaction.PricePerShare,
                result.Transaction.Timestamp,
                result.Transaction.Status),
            result.NewBalance,
            result.Message));
    }

    [HttpPost("sell")]
    public async Task<ActionResult<TradeResponse>> Sell(
        [FromBody] SellStockRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await _tradingService.SellAsync(
            userId,
            request.Symbol,
            request.Quantity,
            cancellationToken);

        return Ok(new TradeResponse(
            result.Success,
            new TransactionDto(
                result.Transaction.TransactionId,
                result.Transaction.Type,
                result.Transaction.Amount,
                result.Transaction.StockSymbol,
                result.Transaction.Quantity,
                result.Transaction.PricePerShare,
                result.Transaction.Timestamp,
                result.Transaction.Status),
            result.NewBalance,
            result.Message));
    }
}

