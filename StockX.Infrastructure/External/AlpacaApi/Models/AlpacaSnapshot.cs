using System.Text.Json.Serialization;

namespace StockX.Infrastructure.External.AlpacaApi.Models;

/// <summary>
/// One entry from GET /v2/stocks/snapshots — keyed by symbol.
/// Contains the current daily bar and previous daily bar so we can
/// compute the day-over-day change percentage.
/// </summary>
public sealed class AlpacaSnapshot
{
    [JsonPropertyName("latestTrade")]
    public SnapshotTrade? LatestTrade { get; set; }

    [JsonPropertyName("latestQuote")]
    public SnapshotQuote? LatestQuote { get; set; }

    [JsonPropertyName("dailyBar")]
    public SnapshotBar? DailyBar { get; set; }

    [JsonPropertyName("prevDailyBar")]
    public SnapshotBar? PrevDailyBar { get; set; }

    // ── Computed helpers ──────────────────────────────────────────────────────

    /// <summary>Mid-point of the latest bid/ask, or last trade price, or daily close.</summary>
    public decimal CurrentPrice
    {
        get
        {
            if (LatestQuote is { AskPrice: > 0, BidPrice: > 0 })
                return (LatestQuote.AskPrice + LatestQuote.BidPrice) / 2m;

            if (LatestTrade is { Price: > 0 })
                return LatestTrade.Price;

            return DailyBar?.Close ?? 0m;
        }
    }

    /// <summary>Day-over-day change % based on previous close → today's close (or mid-quote).</summary>
    public decimal? ChangePercent
    {
        get
        {
            var prev = PrevDailyBar?.Close ?? 0m;
            if (prev == 0m) return null;

            var current = DailyBar?.Close > 0 ? DailyBar.Close : CurrentPrice;
            return Math.Round((current - prev) / prev * 100m, 2);
        }
    }
}

public sealed class SnapshotBar
{
    [JsonPropertyName("o")]
    public decimal Open { get; set; }

    [JsonPropertyName("h")]
    public decimal High { get; set; }

    [JsonPropertyName("l")]
    public decimal Low { get; set; }

    [JsonPropertyName("c")]
    public decimal Close { get; set; }

    [JsonPropertyName("v")]
    public long Volume { get; set; }

    [JsonPropertyName("t")]
    public DateTime Timestamp { get; set; }
}

public sealed class SnapshotTrade
{
    [JsonPropertyName("p")]
    public decimal Price { get; set; }

    [JsonPropertyName("t")]
    public DateTime Timestamp { get; set; }
}

public sealed class SnapshotQuote
{
    [JsonPropertyName("ap")]
    public decimal AskPrice { get; set; }

    [JsonPropertyName("bp")]
    public decimal BidPrice { get; set; }

    [JsonPropertyName("t")]
    public DateTime Timestamp { get; set; }
}
