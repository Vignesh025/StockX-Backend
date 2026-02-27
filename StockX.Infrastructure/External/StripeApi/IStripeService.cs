namespace StockX.Infrastructure.External.StripeApi;

public sealed record StripeCheckoutSession(
    string SessionId,
    string CheckoutUrl,
    string? PaymentIntentId);

public sealed record StripeWebhookEvent(
    string EventId,
    string Type,
    string? PaymentIntentId);

public interface IStripeService
{
    Task<StripeCheckoutSession> CreateDepositCheckoutSessionAsync(
        Guid userId,
        decimal amount,
        string currency,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    void VerifyWebhookSignature(
        string payload,
        string signatureHeader);

    StripeWebhookEvent ParseWebhookEvent(
        string payload);
}

