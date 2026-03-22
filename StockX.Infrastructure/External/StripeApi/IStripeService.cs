namespace StockX.Infrastructure.External.StripeApi;

public sealed record StripeCheckoutSession(
    string SessionId,
    string CheckoutUrl,
    string? PaymentIntentId);

/// <summary>Parsed representation of a Stripe webhook event.</summary>
public sealed record StripeWebhookEvent(
    string EventType,
    string? SessionId,          // cs_xxx — the ID we store in PaymentIntents.IntentId
    string? PaymentIntentId,   // pi_xxx — the actual PaymentIntent ID from Stripe
    string? PaymentIntentStatus,
    decimal? AmountReceived,   // in major units (e.g. dollars, not cents)
    string? Currency,
    string? UserId);

public interface IStripeService
{
    Task<StripeCheckoutSession> CreateDepositCheckoutSessionAsync(
        Guid userId,
        decimal amount,
        string currency,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the Stripe-Signature header against the raw request body.
    /// Returns true when the signature is valid.
    /// </summary>
    bool VerifyWebhookSignature(string rawBody, string signatureHeader);

    /// <summary>
    /// Parses the raw Stripe event JSON into a <see cref="StripeWebhookEvent"/>.
    /// Returns null when the event type is not handled.
    /// </summary>
    StripeWebhookEvent? ParseWebhookEvent(string rawBody);
}

