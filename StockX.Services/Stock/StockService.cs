using StockX.Core.DTOs.Stock;
using StockX.Core.Entities;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Core.Services.Interfaces;

namespace StockX.Services.Stock;

public sealed class StockService : IStockService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStockRepository _stockRepository;

    public StockService(
        IUnitOfWork unitOfWork,
        IStockRepository stockRepository)
    {
        _unitOfWork = unitOfWork;
        _stockRepository = stockRepository;
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
        var stocks = await _unitOfWork.Stocks.GetAllAsync(cancellationToken);
        var top = stocks
            .Take(limit)
            .Select(s => new StockQuote(
                s.Symbol,
                s.Name,
                s.Exchange,
                0m,
                null,
                null,
                s.LastMetadataUpdate))
            .ToList();

        return top;
    }

    public async Task<StockQuote?> GetStockDetailsAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var stocks = await _unitOfWork.Stocks.FindAsync(
            s => s.Symbol == symbol,
            cancellationToken);

        var stock = stocks.FirstOrDefault();

        if (stock is null)
        {
            return null;
        }

        return new StockQuote(
            stock.Symbol,
            stock.Name,
            stock.Exchange,
            0m,
            null,
            null,
            stock.LastMetadataUpdate);
    }
}

