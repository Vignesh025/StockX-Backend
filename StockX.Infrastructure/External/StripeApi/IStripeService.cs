namespace StockX.Infrastructure.External.StripeApi;

public sealed record StripeCheckoutSession(
    string SessionId,
    string CheckoutUrl,
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
}

