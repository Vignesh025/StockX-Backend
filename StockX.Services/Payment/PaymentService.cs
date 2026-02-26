using StockX.Core.DTOs.Payment;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Core.Services.Interfaces;

namespace StockX.Services.Payment;

public sealed class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentIntentRepository _paymentIntentRepository;

    public PaymentService(
        IUnitOfWork unitOfWork,
        IPaymentIntentRepository paymentIntentRepository)
    {
        _unitOfWork = unitOfWork;
        _paymentIntentRepository = paymentIntentRepository;
    }

    public async Task<PaymentInitiationResult> InitiateDepositAsync(
        Guid userId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        var intentId = $"pi_{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;

        var paymentIntent = new PaymentIntent
        {
            IntentId = intentId,
            UserId = userId,
            Amount = amount,
            Currency = "USD",
            Status = PaymentIntentStatus.Pending,
            CreatedAt = now
        };

        await _unitOfWork.PaymentIntents.AddAsync(paymentIntent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var checkoutUrl = intentId;

        return new PaymentInitiationResult(
            checkoutUrl,
            intentId,
            amount,
            paymentIntent.Currency);
    }

    public Task HandleStripeWebhookAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<PaymentIntent?> GetPaymentIntentAsync(
        string intentId,
        CancellationToken cancellationToken = default)
    {
        return await _paymentIntentRepository.GetByIntentIdAsync(
            intentId,
            cancellationToken);
    }

    public async Task UpdatePaymentIntentStatusAsync(
        string intentId,
        PaymentIntentStatus status,
        Guid? transactionId,
        DateTime? completedAt,
        CancellationToken cancellationToken = default)
    {
        var intent = await _paymentIntentRepository.GetByIntentIdAsync(
            intentId,
            cancellationToken);

        if (intent is null)
        {
            throw new InvalidOperationException("Payment intent not found.");
        }

        intent.Status = status;
        intent.TransactionId = transactionId;
        intent.CompletedAt = completedAt;

        _unitOfWork.PaymentIntents.Update(intent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

