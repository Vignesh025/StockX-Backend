using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using StockX.Infrastructure.External.AlpacaApi.Models;

namespace StockX.Infrastructure.External.AlpacaApi;

public sealed class AlpacaApiClient : IAlpacaService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public AlpacaApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        _baseUrl = configuration["Alpaca:BaseUrl"] ??
                   configuration["ALPACA_BASE_URL"] ??
                   "https://paper-api.alpaca.markets";

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
        var url = $"{_baseUrl}/v2/stocks/{encodedSymbol}/quotes/latest";

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
            Symbol = encodedSymbol,
            BidPrice = quoteWrapper.Quote.BidPrice,
            AskPrice = quoteWrapper.Quote.AskPrice,
            LastPrice = quoteWrapper.Quote.AskPrice,
            Timestamp = quoteWrapper.Quote.Timestamp
        };
    }

    private sealed class LatestQuoteResponse
    {
        public LatestQuote? Quote { get; set; }
    }

    private sealed class LatestQuote
    {
        public decimal BidPrice { get; set; }

        public decimal AskPrice { get; set; }

        public DateTime Timestamp { get; set; }
    }
}

