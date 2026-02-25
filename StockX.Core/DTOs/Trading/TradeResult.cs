using StockX.Core.Entities;

namespace StockX.Core.DTOs.Trading;

public sealed record TradeResult(
    bool Success,
    Transaction Transaction,
    decimal NewBalance,
    string Message);

