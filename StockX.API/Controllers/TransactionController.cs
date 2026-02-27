using Microsoft.AspNetCore.Mvc;
using StockX.Core.DTOs.Wallet;
using StockX.Core.Enums;
using StockX.Core.Services.Interfaces;

namespace StockX.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        // Placeholder user id for now.
        return Unauthorized();
    }
}

