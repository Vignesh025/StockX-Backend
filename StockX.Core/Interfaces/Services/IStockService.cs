using StockX.Core.DTOs.Stock;
using StockX.Core.Entities;

namespace StockX.Core.Services.Interfaces;

public interface IStockService
{
    Task<IReadOnlyList<Stock>> SearchStocksAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockQuote>> GetTopStocksAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<StockQuote?> GetStockDetailsAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}

