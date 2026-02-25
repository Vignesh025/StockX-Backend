namespace StockX.Core.DTOs.Stock;

public sealed record StockDto(
    string Symbol,
    string Name,
    string Exchange);

