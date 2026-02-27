using Microsoft.AspNetCore.Mvc;
using StockX.Core.DTOs.Portfolio;
using StockX.Core.Services.Interfaces;

namespace StockX.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PortfolioController : ControllerBase
{
    private readonly ITradingService _tradingService;

    public PortfolioController(ITradingService tradingService)
    {
        _tradingService = tradingService;
    }

    [HttpGet]
    public async Task<ActionResult<PortfolioSummaryDto>> GetPortfolio(
        CancellationToken cancellationToken)
    {
        // Placeholder user id for now.
        return Unauthorized();
    }
}

