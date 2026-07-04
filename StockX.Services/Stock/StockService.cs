using StockX.Core.DTOs.Stock;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Core.Services.Interfaces;
using StockX.Infrastructure.Caching;
using StockX.Infrastructure.External.AlpacaApi;
using StockEntity = StockX.Core.Entities.Stock;

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

    public async Task<IReadOnlyList<StockEntity>> SearchStocksAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = limit <= 0 ? 20 : limit;

        // 1. Try the local DB first (fast path)
        var dbResults = await _stockRepository.SearchAsync(query, limit, cancellationToken);

        if (dbResults.Count >= limit)
            return dbResults;

        // 2. Fall back to Alpaca when DB results are sparse
        try
        {
            var alpacaAssets = await _alpacaService.SearchAssetsAsync(query, limit, cancellationToken);

            if (alpacaAssets.Count == 0)
                return dbResults;

            var now = DateTime.UtcNow;

            var newStocks = alpacaAssets
                .Where(a => !string.IsNullOrWhiteSpace(a.Symbol) && !string.IsNullOrWhiteSpace(a.Name))
                .Select(a => new StockEntity
                {
                    Symbol             = a.Symbol.Trim().ToUpperInvariant(),
                    Name               = a.Name.Trim(),
                    Exchange           = a.Exchange?.Trim() ?? string.Empty,
                    AssetType          = a.AssetType?.Trim() ?? string.Empty,
                    LastMetadataUpdate = now
                })
                .ToList();

            // Upsert into DB (fire-and-forget save; ignore failures)
            _ = _stockRepository.UpsertRangeAsync(newStocks, cancellationToken);

            // Merge DB results with Alpaca results (Alpaca wins for ordering)
            var dbSymbols = dbResults.Select(s => s.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var merged = dbResults.ToList();
            foreach (var s in newStocks.Where(s => !dbSymbols.Contains(s.Symbol)))
                merged.Add(s);

            return merged
                .OrderBy(s => s.Symbol.StartsWith(query.ToUpperInvariant()) ? 0 : 1)
                .ThenBy(s => s.Symbol)
                .Take(limit)
                .ToList();
        }
        catch
        {
            // Alpaca is unavailable — return whatever we have from the DB
            return dbResults;
        }
    }

    public async Task<IReadOnlyList<StockQuote>> GetTopStocksAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = limit <= 0 ? 15 : limit;

        var cacheKey = $"stocks:top:{limit}";

        var cached = await _cacheService.GetAsync<IReadOnlyList<StockQuote>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        // ── 1. Ask Alpaca's screener for today's most-active symbols ─────────
        // The screener returns symbols ranked by trading volume for the current
        // session. On weekends / holidays / plan restrictions it returns nothing,
        // so we fall back to a broad seed universe of US large-cap stocks.
        var liveSymbols = await _alpacaService.GetMostActiveSymbolsAsync(
            top: Math.Max(limit * 3, 50),   // fetch 3× so we have room to filter
            cancellationToken);

        // ── 2. Fallback seed universe ─────────────────────────────────────────
        // Used when the screener is unavailable (market closed / holiday).
        // Kept broad so we always have enough to fill `limit` results.
        var seedSymbols = new[]
        {
            "NVDA", "MSFT", "AAPL", "AMZN", "GOOGL", "META", "TSLA", "AVGO",
            "JPM",  "LLY",  "V",    "WMT",  "UNH",   "XOM",  "MA",   "COST",
            "NFLX", "HD",   "PG",   "JNJ",  "ABBV",  "KO",   "AMD",  "ORCL",
            "CRM",  "CSCO", "ACN",  "MRK",  "TMO",   "BAC",  "PM",   "ADBE",
            "QCOM", "TXN",  "GE",   "IBM",  "NOW",   "INTU", "SPY",  "QQQ",
        };

        var symbolsToQuery = liveSymbols.Count > 0
            ? liveSymbols
            : (IReadOnlyList<string>)seedSymbols;

        // ── 3. Fetch full snapshots in one batch call ─────────────────────────
        var snapshots = await _alpacaService.GetSnapshotsAsync(symbolsToQuery, cancellationToken);

        var now = DateTime.UtcNow;
        var quotes = new List<StockQuote>();

        foreach (var symbol in symbolsToQuery)
        {
            if (!snapshots.TryGetValue(symbol, out var snap))
                continue;

            var price = snap.CurrentPrice;
            if (price <= 0) continue;

            // Try to find the stock name from DB, fall back to symbol
            var dbStocks = await _unitOfWork.Stocks.FindAsync(
                s => s.Symbol == symbol, cancellationToken);
            var dbStock = dbStocks.FirstOrDefault();

            quotes.Add(new StockQuote(
                Symbol:        symbol,
                Name:          dbStock?.Name ?? symbol,
                Exchange:      dbStock?.Exchange ?? string.Empty,
                CurrentPrice:  price,
                MarketCap:     null,
                ChangePercent: snap.ChangePercent,
                DailyVolume:   snap.DailyBar?.Volume,
                LastUpdated:   snap.DailyBar?.Timestamp ?? now));
        }

        // Sort by dollar volume (price × shares traded) — the standard metric
        // for "most active / top stocks". Falls back to price if volume is absent.
        var result = quotes
            .OrderByDescending(q => q.CurrentPrice * (q.DailyVolume ?? 0) > 0
                ? q.CurrentPrice * q.DailyVolume!.Value
                : q.CurrentPrice)
            .Take(limit)
            .ToList();

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10), cancellationToken);

        return result;
    }

    public async Task<StockQuote?> GetStockDetailsAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        var cacheKey = $"stock:quote:{sym}";

        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                // Fetch snapshot (price + change %) in one call
                var snapshots = await _alpacaService.GetSnapshotsAsync(
                    new[] { sym }, cancellationToken);

                if (!snapshots.TryGetValue(sym, out var snap))
                    return null;

                var price = snap.CurrentPrice;
                if (price <= 0) return null;

                // Look up metadata from DB (name, exchange) — upsert if missing
                var dbStocks = await _unitOfWork.Stocks.FindAsync(
                    s => s.Symbol == sym, cancellationToken);
                var dbStock = dbStocks.FirstOrDefault();

                // If not in DB yet, upsert so future searches find it
                if (dbStock is null)
                {
                    dbStock = new StockEntity
                    {
                        Symbol             = sym,
                        Name               = sym,
                        Exchange           = string.Empty,
                        AssetType          = string.Empty,
                        LastMetadataUpdate = DateTime.UtcNow,
                    };
                    _ = _stockRepository.UpsertRangeAsync(
                            new[] { dbStock }, cancellationToken);
                }

                return new StockQuote(
                    Symbol:        sym,
                    Name:          dbStock.Name,
                    Exchange:      dbStock.Exchange,
                    CurrentPrice:  price,
                    MarketCap:     null,
                    ChangePercent: snap.ChangePercent,
                    DailyVolume:   null,
                    LastUpdated:   snap.LatestTrade?.Timestamp   // most recent trade (real-time during market hours)
                                ?? snap.LatestQuote?.Timestamp  // or latest bid/ask quote
                                ?? snap.DailyBar?.Timestamp     // or last daily bar (after-hours / holiday)
                                ?? DateTime.UtcNow);
            },
            TimeSpan.FromMinutes(2),
            cancellationToken);
    }
}

