using StockX.Core.DTOs.Stock;
using StockX.Core.Entities;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Core.Services.Interfaces;
using StockX.Infrastructure.Caching;
using StockX.Infrastructure.External.AlpacaApi;

namespace StockX.Services.Stock;

public sealed class StockService : IStockService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStockRepository _stockRepository;
    private readonly IAlpacaService _alpacaService;
    private readonly ICacheService _cacheService;

    public StockService(
        IUnitOfWork unitOfWork,
        IStockRepository stockRepository,
        IAlpacaService alpacaService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _stockRepository = stockRepository;
        _alpacaService = alpacaService;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyList<Stock>> SearchStocksAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _stockRepository.SearchAsync(query, limit, cancellationToken);
    }

    public async Task<IReadOnlyList<StockQuote>> GetTopStocksAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = limit <= 0 ? 15 : limit;

        var cacheKey = $"stocks:top:{limit}";

        var cached = await _cacheService.GetAsync<IReadOnlyList<StockQuote>>(cacheKey, cancellationToken);

        if (cached is not null)
        {
            return cached;
        }

        var stocks = await _unitOfWork.Stocks.GetAllAsync(cancellationToken);
        var topSymbols = stocks
            .OrderByDescending(s => s.LastMetadataUpdate)
            .Take(limit)
            .ToList();

        var quotes = new List<StockQuote>();

        foreach (var stock in topSymbols)
        {
            var quote = await GetStockDetailsAsync(stock.Symbol, cancellationToken);

            if (quote is not null)
            {
                quotes.Add(quote);
            }
        }

        await _cacheService.SetAsync(cacheKey, quotes, TimeSpan.FromMinutes(10), cancellationToken);

        return quotes;
    }

    public async Task<StockQuote?> GetStockDetailsAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"stock:quote:{symbol.ToUpperInvariant()}";

        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                var stocks = await _unitOfWork.Stocks.FindAsync(
                    s => s.Symbol == symbol,
                    cancellationToken);

                var stock = stocks.FirstOrDefault();

                if (stock is null)
                {
                    return null;
                }

                var quote = await _alpacaService.GetLatestQuoteAsync(symbol, cancellationToken);

                var currentPrice = quote?.LastPrice ?? 0m;

                return new StockQuote(
                    stock.Symbol,
                    stock.Name,
                    stock.Exchange,
                    currentPrice,
                    null,
                    null,
                    quote?.Timestamp ?? stock.LastMetadataUpdate);
            },
            TimeSpan.FromMinutes(2),
            cancellationToken);
    }
}

