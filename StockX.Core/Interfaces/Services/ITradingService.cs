using StockX.Core.DTOs.Portfolio;
using StockX.Core.DTOs.Trading;
using StockX.Core.Entities;

namespace StockX.Core.Services.Interfaces;

public interface ITradingService
{
    Task<TradeResult> BuyAsync(
        Guid userId,
        string symbol,
        decimal quantity,
        CancellationToken cancellationToken = default);

    Task<TradeResult> SellAsync(
        Guid userId,
        string symbol,
        decimal quantity,
        CancellationToken cancellationToken = default);

    Task<PortfolioSummary> GetPortfolioAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

