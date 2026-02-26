using StockX.Core.DTOs.Portfolio;
using StockX.Core.DTOs.Trading;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Core.Services.Interfaces;

namespace StockX.Services.Trading;

public sealed class TradingService : ITradingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHoldingRepository _holdingRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IWalletService _walletService;
    private readonly IStockService _stockService;

    public TradingService(
        IUnitOfWork unitOfWork,
        IHoldingRepository holdingRepository,
        ITransactionRepository transactionRepository,
        IWalletService walletService,
        IStockService stockService)
    {
        _unitOfWork = unitOfWork;
        _holdingRepository = holdingRepository;
        _transactionRepository = transactionRepository;
        _walletService = walletService;
        _stockService = stockService;
    }

    public async Task<TradeResult> BuyAsync(
        Guid userId,
        string symbol,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        var quote = await _stockService.GetStockDetailsAsync(symbol, cancellationToken);

        if (quote is null)
        {
            throw new InvalidOperationException("Stock not found.");
        }

        var price = quote.CurrentPrice;
        var cost = price * quantity;

        var balance = await _walletService.CalculateWalletBalanceAsync(userId, cancellationToken);

        if (balance < cost)
        {
            return new TradeResult(
                false,
                CreateFailedTransaction(userId, symbol, quantity, price, TransactionType.StockBuy),
                balance,
                "Insufficient wallet balance.");
        }

        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            UserId = userId,
            Type = TransactionType.StockBuy,
            Amount = -cost,
            StockSymbol = symbol,
            Quantity = quantity,
            PricePerShare = price,
            Status = TransactionStatus.Completed,
            Timestamp = DateTime.UtcNow
        };

        await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken);

        var holding = await _holdingRepository.GetForUserAndSymbolAsync(
            userId,
            symbol,
            cancellationToken);

        if (holding is null)
        {
            holding = new UserStockHolding
            {
                UserId = userId,
                StockSymbol = symbol,
                TotalQuantity = quantity,
                AverageCostBasis = price,
                LastUpdated = DateTime.UtcNow
            };

            await _unitOfWork.Holdings.AddAsync(holding, cancellationToken);
        }
        else
        {
            var existingQty = holding.TotalQuantity;
            var existingCost = holding.AverageCostBasis;

            var newQty = existingQty + quantity;
            var newAvgCost = (existingQty * existingCost + quantity * price) / newQty;

            holding.TotalQuantity = newQty;
            holding.AverageCostBasis = newAvgCost;
            holding.LastUpdated = DateTime.UtcNow;

            _unitOfWork.Holdings.Update(holding);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var newBalance = balance - cost;

        return new TradeResult(
            true,
            transaction,
            newBalance,
            "Stock purchased successfully.");
    }

    public async Task<TradeResult> SellAsync(
        Guid userId,
        string symbol,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        var holding = await _holdingRepository.GetForUserAndSymbolAsync(
            userId,
            symbol,
            cancellationToken);

        if (holding is null || holding.TotalQuantity < quantity)
        {
            var quoteForError = await _stockService.GetStockDetailsAsync(symbol, cancellationToken);
            var priceForError = quoteForError?.CurrentPrice ?? 0m;

            return new TradeResult(
                false,
                CreateFailedTransaction(userId, symbol, quantity, priceForError, TransactionType.StockSell),
                await _walletService.CalculateWalletBalanceAsync(userId, cancellationToken),
                "Insufficient holdings to sell.");
        }

        var quote = await _stockService.GetStockDetailsAsync(symbol, cancellationToken);

        if (quote is null)
        {
            throw new InvalidOperationException("Stock not found.");
        }

        var price = quote.CurrentPrice;
        var proceeds = price * quantity;

        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            UserId = userId,
            Type = TransactionType.StockSell,
            Amount = proceeds,
            StockSymbol = symbol,
            Quantity = quantity,
            PricePerShare = price,
            Status = TransactionStatus.Completed,
            Timestamp = DateTime.UtcNow
        };

        await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken);

        holding.TotalQuantity -= quantity;

        if (holding.TotalQuantity <= 0)
        {
            _unitOfWork.Holdings.Remove(holding);
        }
        else
        {
            holding.LastUpdated = DateTime.UtcNow;
            _unitOfWork.Holdings.Update(holding);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var newBalance = await _walletService.CalculateWalletBalanceAsync(userId, cancellationToken);

        return new TradeResult(
            true,
            transaction,
            newBalance,
            "Stock sold successfully.");
    }

    public async Task<PortfolioSummary> GetPortfolioAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var holdings = await _holdingRepository.GetByUserAsync(userId, cancellationToken);

        var holdingSummaries = new List<HoldingSummary>();

        foreach (var holding in holdings)
        {
            var quote = await _stockService.GetStockDetailsAsync(holding.StockSymbol, cancellationToken);

            var currentPrice = quote?.CurrentPrice ?? 0m;
            var currentValue = currentPrice * holding.TotalQuantity;
            var totalCost = holding.AverageCostBasis * holding.TotalQuantity;
            var profitLoss = currentValue - totalCost;
            var profitLossPercent = totalCost == 0 ? 0 : profitLoss / totalCost * 100;

            holdingSummaries.Add(
                new HoldingSummary(
                    holding.StockSymbol,
                    quote?.Name ?? holding.StockSymbol,
                    holding.TotalQuantity,
                    holding.AverageCostBasis,
                    currentPrice,
                    currentValue,
                    profitLoss,
                    profitLossPercent));
        }

        var portfolioTotalValue = holdingSummaries.Sum(h => h.CurrentValue);
        var portfolioTotalCost = holdingSummaries.Sum(h => h.AverageCostBasis * h.Quantity);
        var portfolioProfitLoss = portfolioTotalValue - portfolioTotalCost;

        return new PortfolioSummary(
            holdingSummaries,
            portfolioTotalValue,
            portfolioTotalCost,
            portfolioProfitLoss);
    }

    private static Transaction CreateFailedTransaction(
        Guid userId,
        string symbol,
        decimal quantity,
        decimal price,
        TransactionType type)
    {
        return new Transaction
        {
            TransactionId = Guid.Empty,
            UserId = userId,
            Type = type,
            Amount = 0m,
            StockSymbol = symbol,
            Quantity = quantity,
            PricePerShare = price,
            Status = TransactionStatus.Failed,
            Timestamp = DateTime.UtcNow,
            Notes = "Failed trade."
        };
    }
}

