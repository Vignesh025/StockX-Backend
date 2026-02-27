using Microsoft.AspNetCore.Mvc;
using StockX.Core.DTOs.Stock;
using StockX.Core.Services.Interfaces;

namespace StockX.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StockController : ControllerBase
{
    private readonly IStockService _stockService;

    public StockController(IStockService stockService)
    {
        _stockService = stockService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<StockDto>>> Search(
        [FromQuery] string query,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var stocks = await _stockService.SearchStocksAsync(query, limit, cancellationToken);

        var result = stocks
            .Select(s => new StockDto(s.Symbol, s.Name, s.Exchange))
            .ToList();

        return Ok(result);
    }

    [HttpGet("top")]
    public async Task<ActionResult<IReadOnlyList<StockQuote>>> GetTop(
        [FromQuery] int limit = 15,
        CancellationToken cancellationToken = default)
    {
        var quotes = await _stockService.GetTopStocksAsync(limit, cancellationToken);
        return Ok(quotes);
    }

    [HttpGet("{symbol}")]
    public async Task<ActionResult<StockDetailDto>> GetDetails(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var quote = await _stockService.GetStockDetailsAsync(symbol, cancellationToken);

        if (quote is null)
        {
            return NotFound();
        }

        var dto = new StockDetailDto(
            quote.Symbol,
            quote.Name,
            quote.Exchange,
            quote.CurrentPrice,
            quote.LastUpdated);

        return Ok(dto);
    }
}

