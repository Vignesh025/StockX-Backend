using StockX.Core.Enums;

namespace StockX.Core.Entities;

public sealed class Transaction
{
    public Guid TransactionId { get; set; }

    public Guid UserId { get; set; }

    public TransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public string? StockSymbol { get; set; }

    public decimal? Quantity { get; set; }

    public decimal? PricePerShare { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public DateTime Timestamp { get; set; }

    public string? Notes { get; set; }

    public User? User { get; set; }

    public Stock? Stock { get; set; }
}

