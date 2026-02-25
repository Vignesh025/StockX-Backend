namespace StockX.Core.Entities;

public sealed class Stock
{
    public string Symbol { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Exchange { get; set; } = string.Empty;

    public string AssetType { get; set; } = string.Empty;

    public DateTime LastMetadataUpdate { get; set; }

    public ICollection<UserStockHolding> UserStockHoldings { get; set; } = new List<UserStockHolding>();

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

