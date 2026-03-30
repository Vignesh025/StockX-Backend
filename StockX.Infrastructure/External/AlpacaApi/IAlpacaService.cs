using StockX.Infrastructure.External.AlpacaApi.Models;

namespace StockX.Infrastructure.External.AlpacaApi;

public interface IAlpacaService
{
    Task<IReadOnlyList<AlpacaAsset>> GetAssetsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches Alpaca for active US equity assets whose symbol or name
    /// contains <paramref name="query"/>. Returns at most <paramref name="limit"/> results.
    /// </summary>
    Task<IReadOnlyList<AlpacaAsset>> SearchAssetsAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a snapshot (daily bar, prev daily bar, latest quote) for each
    /// of the requested symbols in a single API call.
    /// </summary>
    Task<IReadOnlyDictionary<string, AlpacaSnapshot>> GetSnapshotsAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    Task<AlpacaQuote?> GetLatestQuoteAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}


