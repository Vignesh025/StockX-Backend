using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using StockX.Infrastructure.External.AlpacaApi.Models;

namespace StockX.Infrastructure.External.AlpacaApi;

public sealed class AlpacaApiClient : IAlpacaService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _dataUrl;

    public AlpacaApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        // Trading API  (orders, assets, positions, etc.)
        _baseUrl = configuration["Alpaca:BaseUrl"] ??
                   configuration["ALPACA_BASE_URL"] ??
                   "https://paper-api.alpaca.markets";

        // Market Data API (quotes, bars, trades, etc.)
        _dataUrl = configuration["Alpaca:DataUrl"] ??
                   configuration["ALPACA_DATA_URL"] ??
                   "https://data.alpaca.markets";

        var apiKey = configuration["Alpaca:ApiKey"] ??
                     configuration["ALPACA_API_KEY"];
        var secretKey = configuration["Alpaca:SecretKey"] ??
                        configuration["ALPACA_SECRET_KEY"];

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("APCA-API-KEY-ID", apiKey);
        }

        if (!string.IsNullOrWhiteSpace(secretKey))
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("APCA-API-SECRET-KEY", secretKey);
        }
    }

    public async Task<IReadOnlyList<AlpacaAsset>> GetAssetsAsync(
        CancellationToken cancellationToken = default)
    {
        // Assets list lives on the trading API
        var url = $"{_baseUrl}/v2/assets?status=active&asset_class=us_equity";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var assets = await response.Content.ReadFromJsonAsync<List<AlpacaAsset>>(cancellationToken: cancellationToken);
        return assets ?? new List<AlpacaAsset>();
    }

    public async Task<IReadOnlyList<AlpacaAsset>> SearchAssetsAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<AlpacaAsset>();

        // Alpaca supports ?search= which matches against name (not symbol).
        // We fetch with search=query AND also try an exact symbol lookup
        // so that typing "AAPL" still works even when Alpaca's name search
        // doesn't match it.
        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var url = $"{_baseUrl}/v2/assets?status=active&asset_class=us_equity&search={encodedQuery}";

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return Array.Empty<AlpacaAsset>();

        var assets = await response.Content.ReadFromJsonAsync<List<AlpacaAsset>>(cancellationToken: cancellationToken)
                     ?? new List<AlpacaAsset>();

        var q = query.Trim().ToUpperInvariant();

        // Prioritise: exact symbol match → starts-with symbol → name contains → rest
        var ordered = assets
            .Where(a => !string.IsNullOrWhiteSpace(a.Symbol) && !string.IsNullOrWhiteSpace(a.Name) && a.Tradable)
            .OrderBy(a =>
            {
                var sym = a.Symbol.ToUpperInvariant();
                if (sym == q) return 0;
                if (sym.StartsWith(q)) return 1;
                if (a.Name.ToUpperInvariant().Contains(q)) return 2;
                return 3;
            })
            .Take(limit)
            .ToList();

        return ordered;
    }

    public async Task<IReadOnlyDictionary<string, AlpacaSnapshot>> GetSnapshotsAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        var symbolList = symbols
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        if (symbolList.Count == 0)
            return new Dictionary<string, AlpacaSnapshot>();

        // Batch endpoint: GET /v2/stocks/snapshots?symbols=AAPL,MSFT,...
        // Use default SIP feed (widest coverage). IEX has limited symbols.
        var joined = string.Join(",", symbolList.Select(Uri.EscapeDataString));
        var url = $"{_dataUrl}/v2/stocks/snapshots?symbols={joined}";

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new Dictionary<string, AlpacaSnapshot>();

        var dict = await response.Content
            .ReadFromJsonAsync<Dictionary<string, AlpacaSnapshot>>(cancellationToken: cancellationToken)
            ?? new Dictionary<string, AlpacaSnapshot>();

        return dict;
    }

    public async Task<AlpacaQuote?> GetLatestQuoteAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));

        // Use the snapshot endpoint — it gives us current price AND prev close
        // so we can compute change % in one round-trip.
        var snapshots = await GetSnapshotsAsync(new[] { symbol }, cancellationToken);

        var sym = symbol.Trim().ToUpperInvariant();
        if (!snapshots.TryGetValue(sym, out var snap))
            return null;

        return new AlpacaQuote
        {
            Symbol       = sym,
            BidPrice     = snap.LatestQuote?.BidPrice ?? 0m,
            AskPrice     = snap.LatestQuote?.AskPrice ?? 0m,
            LastPrice    = snap.CurrentPrice,
            ChangePercent = snap.ChangePercent,
            Timestamp    = snap.LatestQuote?.Timestamp
                        ?? snap.DailyBar?.Timestamp
                        ?? DateTime.UtcNow,
        };
    }

    // ── Deserialization models ────────────────────────────────────────────────
    // Alpaca returns abbreviated field names ("ap", "bp", "t") — the
    // [JsonPropertyName] attributes tell System.Text.Json how to map them.

    private sealed class LatestQuoteResponse
    {
        [JsonPropertyName("quote")]
        public LatestQuote? Quote { get; set; }
    }

    private sealed class LatestQuote
    {
        [JsonPropertyName("bp")]
        public decimal BidPrice { get; set; }

        [JsonPropertyName("ap")]
        public decimal AskPrice { get; set; }

        [JsonPropertyName("t")]
        public DateTime Timestamp { get; set; }
    }
}


