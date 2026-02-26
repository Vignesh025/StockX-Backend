using Microsoft.Extensions.DependencyInjection;
using StockX.Core.Interfaces;
using StockX.Core.Services.Interfaces;
using StockX.Services.Admin;
using StockX.Services.Auth;
using StockX.Services.Payment;
using StockX.Services.Stock;
using StockX.Services.Trading;
using StockX.Services.Wallet;

namespace StockX.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();

        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<ITradingService, TradingService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }
}

