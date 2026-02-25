using StockX.Core.DTOs.Payment;
using StockX.Core.Entities;
using StockX.Core.Enums;

namespace StockX.Core.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentInitiationResult> InitiateDepositAsync(
        Guid userId,
        decimal amount,
        CancellationToken cancellationToken = default);

    Task HandleStripeWebhookAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default);

    Task<PaymentIntent?> GetPaymentIntentAsync(
        string intentId,
        CancellationToken cancellationToken = default);

    Task UpdatePaymentIntentStatusAsync(
        string intentId,
        PaymentIntentStatus status,
        Guid? transactionId,
        DateTime? completedAt,
        CancellationToken cancellationToken = default);
}

