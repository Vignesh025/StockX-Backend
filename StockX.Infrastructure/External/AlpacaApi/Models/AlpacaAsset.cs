namespace StockX.Infrastructure.External.AlpacaApi.Models;

public sealed class AlpacaAsset
{
    public string Symbol { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Exchange { get; set; } = string.Empty;

    public string AssetType { get; set; } = string.Empty;

    public bool Tradable { get; set; }
}

