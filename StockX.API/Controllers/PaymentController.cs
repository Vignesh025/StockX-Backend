using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Services.Interfaces;
using StockX.Infrastructure.External.StripeApi;

namespace StockX.API.Controllers;

[ApiController]
[Route("api/payment")]
public sealed class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IStripeService _stripeService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        IStripeService stripeService,
        IUnitOfWork unitOfWork,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _stripeService  = stripeService;
        _unitOfWork     = unitOfWork;
        _logger         = logger;
    }

    /// <summary>
    /// Stripe webhook endpoint.
    /// Stripe posts events here when a payment succeeds or fails.
    /// Must NOT have [Authorize] — Stripe calls this without any JWT.
    /// Security is provided by the HMAC-SHA256 signature verification instead.
    /// </summary>
    [HttpPost("webhook/stripe")]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        // 1. Read the raw body (must be raw bytes — do NOT use [FromBody])
        string rawBody;
        using (var reader = new StreamReader(Request.Body))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
        }

        // 2. Verify the Stripe-Signature header
        if (!Request.Headers.TryGetValue("Stripe-Signature", out var sigHeader))
        {
            _logger.LogWarning("Stripe webhook received without Stripe-Signature header.");
            return BadRequest("Missing Stripe-Signature header.");
        }

        if (!_stripeService.VerifyWebhookSignature(rawBody, sigHeader.ToString()))
        {
            _logger.LogWarning("Stripe webhook signature verification failed.");
            return BadRequest("Invalid signature.");
        }

        // 3. Parse the event — returns null for event types we don't handle
        var stripeEvent = _stripeService.ParseWebhookEvent(rawBody);

        if (stripeEvent is null)
        {
            // Acknowledge unhandled events so Stripe doesn't keep retrying
            return Ok();
        }

        _logger.LogInformation(
            "Stripe webhook received: {EventType} for PaymentIntent {PaymentIntentId}",
            stripeEvent.EventType,
            stripeEvent.PaymentIntentId);

        // 4. Route to the appropriate handler
        if (stripeEvent.EventType == "checkout.session.completed")
        {
            await HandlePaymentSucceededAsync(stripeEvent, cancellationToken);
        }
        else if (stripeEvent.EventType == "payment_intent.payment_failed")
        {
            await HandlePaymentFailedAsync(stripeEvent, cancellationToken);
        }

        // Always return 200 so Stripe knows we received it
        return Ok();
    }

    // ── Private handlers ──────────────────────────────────────────────────────

    private async Task HandlePaymentSucceededAsync(
        StripeWebhookEvent stripeEvent,
        CancellationToken cancellationToken)
    {
        // We store the checkout session ID (cs_xxx) as IntentId in the DB
        var lookupId = stripeEvent.SessionId;

        if (string.IsNullOrWhiteSpace(lookupId))
        {
            _logger.LogWarning("checkout.session.completed event missing SessionId.");
            return;
        }

        var intent = await _paymentService.GetPaymentIntentAsync(lookupId, cancellationToken);

        if (intent is null)
        {
            _logger.LogWarning(
                "No PaymentIntent found in DB for session {SessionId}. Skipping.",
                lookupId);
            return;
        }

        // Guard against duplicate delivery
        if (intent.Status == PaymentIntentStatus.Completed)
        {
            _logger.LogInformation(
                "Session {SessionId} already completed. Ignoring duplicate webhook.",
                lookupId);
            return;
        }

        var now = DateTime.UtcNow;

        // Create the Deposit transaction that credits the user's wallet
        var depositTransaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            UserId        = intent.UserId,
            Type          = TransactionType.Deposit,
            Amount        = stripeEvent.AmountReceived ?? intent.Amount,
            Status        = TransactionStatus.Completed,
            Timestamp     = now
        };

        await _unitOfWork.Transactions.AddAsync(depositTransaction, cancellationToken);

        await _paymentService.UpdatePaymentIntentStatusAsync(
            lookupId,
            PaymentIntentStatus.Completed,
            depositTransaction.TransactionId,
            now,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deposit of {Amount} {Currency} credited to user {UserId}.",
            depositTransaction.Amount,
            stripeEvent.Currency,
            intent.UserId);
    }

    private async Task HandlePaymentFailedAsync(
        StripeWebhookEvent stripeEvent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stripeEvent.PaymentIntentId))
            return;

        var intent = await _paymentService.GetPaymentIntentAsync(
            stripeEvent.PaymentIntentId, cancellationToken);

        if (intent is null || intent.Status != PaymentIntentStatus.Pending)
            return;

        await _paymentService.UpdatePaymentIntentStatusAsync(
            stripeEvent.PaymentIntentId,
            PaymentIntentStatus.Failed,
            null,
            DateTime.UtcNow,
            cancellationToken);

        _logger.LogWarning(
            "Payment failed for PaymentIntent {Id}, user {UserId}.",
            stripeEvent.PaymentIntentId,
            intent.UserId);
    }
}
