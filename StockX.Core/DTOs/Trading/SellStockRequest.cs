namespace StockX.Core.DTOs.Trading;

public sealed record SellStockRequest(
    string Symbol,
    decimal Quantity);

