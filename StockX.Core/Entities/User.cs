using StockX.Core.Enums;

namespace StockX.Core.Entities;

public sealed class User
{
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.NormalUser;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<UserStockHolding> StockHoldings { get; set; } = new List<UserStockHolding>();

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public ICollection<PaymentIntent> PaymentIntents { get; set; } = new List<PaymentIntent>();
}

