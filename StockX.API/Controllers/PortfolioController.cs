using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var summary = await _tradingService.GetPortfolioAsync(userId, cancellationToken);

        var holdings = summary.Holdings
            .Select(h => new HoldingDto(
                h.Symbol,
                h.Name,
                h.Quantity,
                h.AverageCostBasis,
                h.CurrentPrice,
                h.CurrentValue,
                h.ProfitLoss,
                h.ProfitLossPercent))
            .ToList();

        var dto = new PortfolioSummaryDto(
            holdings,
            summary.TotalValue,
            summary.TotalCost,
            summary.TotalProfitLoss);

        return Ok(dto);
    }
}

