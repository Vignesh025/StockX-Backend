namespace StockX.Core.DTOs.Trading;

public sealed record BuyStockRequest(
    string Symbol,
    decimal Quantity);

