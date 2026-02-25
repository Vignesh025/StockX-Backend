namespace StockX.Core.DTOs.Admin;

public sealed record AdminDashboardDto(
    int TotalUsers,
    int ActiveUsers,
    int AdminUsers,
    decimal TotalDeposits,
    decimal TotalTradingVolume);

