using StockX.Infrastructure.External.AlpacaApi.Models;

namespace StockX.Infrastructure.External.AlpacaApi;

public interface IAlpacaService
{
    Task<IReadOnlyList<AlpacaAsset>> GetAssetsAsync(
        CancellationToken cancellationToken = default);

    Task<AlpacaQuote?> GetLatestQuoteAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}

