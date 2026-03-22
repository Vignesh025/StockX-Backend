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

    public async Task<AlpacaQuote?> GetLatestQuoteAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        var encodedSymbol = Uri.EscapeDataString(symbol.ToUpperInvariant());

        // Quotes live on the market DATA API, not the trading API
        var url = $"{_dataUrl}/v2/stocks/{encodedSymbol}/quotes/latest";

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var quoteWrapper = await response.Content.ReadFromJsonAsync<LatestQuoteResponse>(cancellationToken: cancellationToken);

        if (quoteWrapper?.Quote is null)
        {
            return null;
        }

        return new AlpacaQuote
        {
            Symbol    = encodedSymbol,
            BidPrice  = quoteWrapper.Quote.BidPrice,
            AskPrice  = quoteWrapper.Quote.AskPrice,
            // Use mid-point as the effective "last price" so buy/sell use a fair value
            LastPrice = (quoteWrapper.Quote.BidPrice + quoteWrapper.Quote.AskPrice) / 2m,
            Timestamp = quoteWrapper.Quote.Timestamp
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

