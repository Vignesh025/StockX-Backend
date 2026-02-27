namespace StockX.Infrastructure.External.AlpacaApi.Models;

public sealed class AlpacaQuote
{
    public string Symbol { get; set; } = string.Empty;

    public decimal BidPrice { get; set; }

    public decimal AskPrice { get; set; }

    public decimal LastPrice { get; set; }

    public DateTime Timestamp { get; set; }
}

