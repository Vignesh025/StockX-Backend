namespace StockX.Core.Entities;

public sealed class UserStockHolding
{
    public Guid UserId { get; set; }

    public string StockSymbol { get; set; } = string.Empty;

    public decimal TotalQuantity { get; set; }

    public decimal AverageCostBasis { get; set; }

    public DateTime LastUpdated { get; set; }

    public User? User { get; set; }

    public Stock? Stock { get; set; }
}

