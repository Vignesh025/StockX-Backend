namespace StockX.Core.DTOs.Wallet;

public sealed record WalletBalanceSummary(
    decimal Balance,
    DateTime LastUpdated);

