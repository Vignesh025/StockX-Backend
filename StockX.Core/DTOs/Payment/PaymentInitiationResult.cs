namespace StockX.Core.DTOs.Payment;

public sealed record PaymentInitiationResult(
    string CheckoutUrl,
    string PaymentIntentId,
    decimal Amount,
    string Currency);

