using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockX.Core.Entities;
using StockX.Infrastructure.External.AlpacaApi;
using StockX.Infrastructure.External.AlpacaApi.Models;
using StockX.Infrastructure.Persistence.Context;

namespace StockX.Infrastructure.Persistence.Seeding;

public static class StockSeeder
{
    /// <summary>
    /// Seeds the Stocks table from the Alpaca API if the table is empty.
    /// Call this once at application startup after migrations.
    /// </summary>
    public static async Task SeedStocksAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alpaca = scope.ServiceProvider.GetRequiredService<IAlpacaService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        // Only seed if the table has fewer than 500 stocks — this threshold
        // catches both an empty table and a previous partial/failed seed.
        const int MinStocksThreshold = 500;
        var stockCount = await db.Stocks.CountAsync(cancellationToken);
        if (stockCount >= MinStocksThreshold)
        {
            logger.LogInformation(
                "Stocks table has {Count} rows — skipping seeding (threshold: {Min}).",
                stockCount, MinStocksThreshold);
            return;
        }

        logger.LogInformation(
            "Stocks table has only {Count} rows (threshold: {Min}). Fetching assets from Alpaca...",
            stockCount, MinStocksThreshold);

        IReadOnlyList<AlpacaAsset> assets;
        try
        {
            assets = await alpaca.GetAssetsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch assets from Alpaca. Stocks table will remain empty.");
            return;
        }

        if (assets.Count == 0)
        {
            logger.LogWarning("Alpaca returned 0 assets. Stocks table will remain empty.");
            return;
        }

        var now = DateTime.UtcNow;

        var incoming = assets
            .Where(a => !string.IsNullOrWhiteSpace(a.Symbol) && !string.IsNullOrWhiteSpace(a.Name))
            .GroupBy(a => a.Symbol.Trim().ToUpperInvariant())
            .Select(g => new Stock
            {
                Symbol             = g.Key,
                Name               = g.First().Name.Trim(),
                Exchange           = g.First().Exchange?.Trim() ?? string.Empty,
                AssetType          = g.First().AssetType?.Trim() ?? string.Empty,
                LastMetadataUpdate = now
            })
            .ToList();

        // Skip symbols that are already in the DB to avoid duplicate-key errors
        var existingSymbols = await db.Stocks
            .Select(s => s.Symbol)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        var newStocks = incoming.Where(s => !existingSymbols.Contains(s.Symbol)).ToList();

        if (newStocks.Count == 0)
        {
            logger.LogInformation("All fetched assets already exist in the database — no new rows inserted.");
            return;
        }

        await db.Stocks.AddRangeAsync(newStocks, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} new stocks into the database ({Total} total from Alpaca).",
            newStocks.Count, incoming.Count);
    }
}
