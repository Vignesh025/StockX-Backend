namespace StockX.Core.DTOs.Portfolio;

public sealed record PortfolioSummary(
    IReadOnlyList<HoldingSummary> Holdings,
    decimal TotalValue,
    decimal TotalCost,
    decimal TotalProfitLoss);

