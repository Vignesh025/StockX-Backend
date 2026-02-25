using StockX.Core.Enums;

namespace StockX.Core.Entities;

public sealed class PaymentIntent
{
    public string IntentId { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.Pending;

    public Guid? TransactionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public User? User { get; set; }

    public Transaction? Transaction { get; set; }
}

