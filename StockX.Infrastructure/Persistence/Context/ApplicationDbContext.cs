using Microsoft.EntityFrameworkCore;
using StockX.Core.Entities;

namespace StockX.Infrastructure.Persistence.Context;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Stock> Stocks => Set<Stock>();

    public DbSet<UserStockHolding> UserStockHoldings => Set<UserStockHolding>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUsers(modelBuilder);
        ConfigureStocks(modelBuilder);
        ConfigureUserStockHoldings(modelBuilder);
        ConfigureTransactions(modelBuilder);
        ConfigurePaymentIntents(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<User>();

        entity.ToTable("Users");

        entity.HasKey(u => u.UserId);

        entity.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(320);

        entity.HasIndex(u => u.Email)
            .IsUnique();

        entity.Property(u => u.PasswordHash)
            .IsRequired();

        entity.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.Property(u => u.IsActive)
            .HasDefaultValue(true);

        entity.Property(u => u.CreatedAt)
            .IsRequired();

        entity.Property(u => u.UpdatedAt)
            .IsRequired();
    }

    private static void ConfigureStocks(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Stock>();

        entity.ToTable("Stocks");

        entity.HasKey(s => s.Symbol);

        entity.Property(s => s.Symbol)
            .HasMaxLength(32);

        entity.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(256);

        entity.Property(s => s.Exchange)
            .IsRequired()
            .HasMaxLength(64);

        entity.Property(s => s.AssetType)
            .IsRequired()
            .HasMaxLength(64);
    }

    private static void ConfigureUserStockHoldings(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserStockHolding>();

        entity.ToTable("UserStockHoldings");

        entity.HasKey(h => new { h.UserId, h.StockSymbol });

        entity.Property(h => h.StockSymbol)
            .HasMaxLength(32);

        entity.Property(h => h.TotalQuantity)
            .IsRequired()
            .HasColumnType("decimal(18,4)");

        entity.Property(h => h.AverageCostBasis)
            .IsRequired()
            .HasColumnType("decimal(18,4)");

        entity.Property(h => h.LastUpdated)
            .IsRequired();

        entity.HasOne(h => h.User)
            .WithMany(u => u.StockHoldings)
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(h => h.Stock)
            .WithMany(s => s.UserStockHoldings)
            .HasForeignKey(h => h.StockSymbol)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureTransactions(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Transaction>();

        entity.ToTable("Transactions");

        entity.HasKey(t => t.TransactionId);

        entity.Property(t => t.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        entity.Property(t => t.Quantity)
            .HasColumnType("decimal(18,4)");

        entity.Property(t => t.PricePerShare)
            .HasColumnType("decimal(18,4)");

        entity.Property(t => t.StockSymbol)
            .HasMaxLength(32);

        entity.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);  

        entity.Property(t => t.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.Property(t => t.Timestamp)
            .IsRequired();

        entity.HasOne(t => t.User)
            .WithMany(u => u.Transactions)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(t => t.Stock)
            .WithMany(s => s.Transactions)
            .HasForeignKey(t => t.StockSymbol)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePaymentIntents(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PaymentIntent>();

        entity.ToTable("PaymentIntents");

        entity.HasKey(p => p.IntentId);

        entity.Property(p => p.IntentId)
            .HasMaxLength(128);

        entity.Property(p => p.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        entity.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue("USD");

        entity.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.Property(p => p.CreatedAt)
            .IsRequired();

        entity.HasOne(p => p.User)
            .WithMany(u => u.PaymentIntents)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(p => p.Transaction)
            .WithOne()
            .HasForeignKey<PaymentIntent>(p => p.TransactionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

