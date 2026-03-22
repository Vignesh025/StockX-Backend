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

        // Only seed if the table is empty
        var hasStocks = await db.Stocks.AnyAsync(cancellationToken);
        if (hasStocks)
        {
            logger.LogInformation("Stocks table already has data — skipping seeding.");
            return;
        }

        logger.LogInformation("Stocks table is empty. Fetching assets from Alpaca to seed...");

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

        var stocks = assets
            .Where(a => !string.IsNullOrWhiteSpace(a.Symbol) && !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => new Stock
            {
                Symbol = a.Symbol.Trim().ToUpperInvariant(),
                Name = a.Name.Trim(),
                Exchange = a.Exchange.Trim(),
                AssetType = a.AssetType.Trim(),
                LastMetadataUpdate = now
            })
            // Guard against duplicates in the Alpaca response
            .GroupBy(s => s.Symbol)
            .Select(g => g.First())
            .ToList();

        await db.Stocks.AddRangeAsync(stocks, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} stocks into the database.", stocks.Count);
    }
}
