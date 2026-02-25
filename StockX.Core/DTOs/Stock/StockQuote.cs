namespace StockX.Core.DTOs.Stock;

public sealed record StockQuote(
    string Symbol,
    string Name,
    string Exchange,
    decimal CurrentPrice,
    decimal? MarketCap,
    decimal? ChangePercent,
    DateTime LastUpdated);

