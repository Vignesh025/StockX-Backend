using StockX.Core.Enums;

namespace StockX.Core.DTOs.Wallet;

public sealed record TransactionDto(
    Guid Id,
    TransactionType Type,
    decimal Amount,
    string? Symbol,
    decimal? Quantity,
    decimal? PricePerShare,
    DateTime Timestamp,
    TransactionStatus Status);

