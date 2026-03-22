using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using StockX.Core.DTOs.Wallet;
using StockX.Core.Enums;
using StockX.Core.Services.Interfaces;

namespace StockX.API.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionController : ControllerBase
{
    private readonly IWalletService _walletService;

    public TransactionController(IWalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> GetTransactions(
        [FromQuery] string type = "all",
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        TransactionType? filterType = type.ToLowerInvariant() switch
        {
            "deposit" => TransactionType.Deposit,
            "trade" => null, // include both buy and sell
            "stock_buy" => TransactionType.StockBuy,
            "stock_sell" => TransactionType.StockSell,
            _ => null
        };

        var transactions = await _walletService.GetTransactionsAsync(
            userId,
            filterType,
            limit,
            offset,
            cancellationToken);

        var dtos = transactions
            .Select(t => new TransactionDto(
                t.TransactionId,
                t.Type,
                t.Amount,
                t.StockSymbol,
                t.Quantity,
                t.PricePerShare,
                t.Timestamp,
                t.Status))
            .ToList();

        return Ok(dtos);
    }
}

