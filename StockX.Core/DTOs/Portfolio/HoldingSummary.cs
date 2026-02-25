namespace StockX.Core.DTOs.Portfolio;

public sealed record HoldingSummary(
    string Symbol,
    string Name,
    decimal Quantity,
    decimal AverageCostBasis,
    decimal CurrentPrice,
    decimal CurrentValue,
    decimal ProfitLoss,
    decimal ProfitLossPercent);

