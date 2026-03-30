namespace StockX.Infrastructure.External.AlpacaApi.Models;

public sealed class AlpacaQuote
{
    public string Symbol { get; set; } = string.Empty;

    public decimal BidPrice { get; set; }

    public decimal AskPrice { get; set; }

    public decimal LastPrice { get; set; }

    /// <summary>Day-over-day change % from previous close. Null if unavailable.</summary>
    public decimal? ChangePercent { get; set; }

    public DateTime Timestamp { get; set; }
}

