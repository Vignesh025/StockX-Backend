using Microsoft.Extensions.Configuration;
using StockX.Core.DTOs.Payment;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Core.Services.Interfaces;
using StockX.Infrastructure.External.StripeApi;

namespace StockX.Services.Payment;

public sealed class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentIntentRepository _paymentIntentRepository;
    private readonly IStripeService _stripeService;
    private readonly IConfiguration _configuration;

    public PaymentService(
        IUnitOfWork unitOfWork,
        IPaymentIntentRepository paymentIntentRepository,
        IStripeService stripeService,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _paymentIntentRepository = paymentIntentRepository;
        _stripeService = stripeService;
        _configuration = configuration;
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

        var currency = "usd";

        var successUrl = _configuration["Stripe:SuccessUrl"] ??
                         "https://example.com/success";
        var cancelUrl = _configuration["Stripe:CancelUrl"] ??
                        "https://example.com/cancel";

        var session = await _stripeService.CreateDepositCheckoutSessionAsync(
            userId,
            amount,
            currency,
            successUrl,
            cancelUrl,
            cancellationToken);

        var now = DateTime.UtcNow;

        var paymentIntent = new PaymentIntent
        {
            IntentId = session.PaymentIntentId ?? session.SessionId,
            UserId = userId,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            Status = PaymentIntentStatus.Pending,
            CreatedAt = now
        };

        await _unitOfWork.PaymentIntents.AddAsync(paymentIntent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PaymentInitiationResult(
            session.CheckoutUrl,
            paymentIntent.IntentId,
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

