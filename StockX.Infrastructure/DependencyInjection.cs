using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Infrastructure.Caching;
using StockX.Infrastructure.External.AlpacaApi;
using StockX.Infrastructure.External.StripeApi;
using StockX.Infrastructure.Persistence.Context;
using StockX.Infrastructure.Persistence.Repositories;
using StockX.Infrastructure.Persistence.UnitOfWork;

namespace StockX.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
    
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' not found in configuration.");
            }
            
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10));
            });
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IHoldingRepository, HoldingRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IPaymentIntentRepository, PaymentIntentRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddMemoryCache();
        services.AddScoped<ICacheService, MemoryCacheService>();

        services.AddHttpClient<IAlpacaService, AlpacaApiClient>();
        services.AddHttpClient<IStripeService, StripePaymentService>();

        return services;
    }
}

