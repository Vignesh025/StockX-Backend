using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace StockX.Infrastructure.External.StripeApi;

public sealed class StripePaymentService : IStripeService
{
    private const string StripeApiBaseUrl = "https://api.stripe.com";

    private readonly HttpClient _httpClient;
    private readonly string _secretKey;
    private readonly string _webhookSecret;

    public StripePaymentService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        _secretKey = configuration["Stripe:SecretKey"] ??
                     configuration["STRIPE_SECRET_KEY"] ??
                     string.Empty;

        _webhookSecret = configuration["Stripe:WebhookSecret"] ??
                         configuration["STRIPE_WEBHOOK_SECRET"] ??
                         string.Empty;

        if (!string.IsNullOrWhiteSpace(_secretKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _secretKey);
        }
    }

    // ── Checkout Session ──────────────────────────────────────────────────────

    public async Task<StripeCheckoutSession> CreateDepositCheckoutSessionAsync(
        Guid userId,
        decimal amount,
        string currency,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.", nameof(currency));
        if (string.IsNullOrWhiteSpace(successUrl))
            throw new ArgumentException("Success URL is required.", nameof(successUrl));
        if (string.IsNullOrWhiteSpace(cancelUrl))
            throw new ArgumentException("Cancel URL is required.", nameof(cancelUrl));

        EnsureConfigured();

        var unitAmount = ToMinorUnits(amount, currency);

        var form = new Dictionary<string, string>
        {
            ["mode"]                                               = "payment",
            ["success_url"]                                        = successUrl,
            ["cancel_url"]                                         = cancelUrl,
            ["client_reference_id"]                                = userId.ToString(),
            ["metadata[userId]"]                                   = userId.ToString(),
            ["line_items[0][price_data][currency]"]                = currency.ToLowerInvariant(),
            ["line_items[0][price_data][product_data][name]"]      = "Wallet Deposit",
            ["line_items[0][price_data][unit_amount]"]             = unitAmount.ToString(CultureInfo.InvariantCulture),
            ["line_items[0][quantity]"]                            = "1",
            ["payment_intent_data[metadata][userId]"]              = userId.ToString()
        };

        using var content  = new FormUrlEncodedContent(form);
        using var response = await _httpClient.PostAsync(
            $"{StripeApiBaseUrl}/v1/checkout/sessions", content, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Stripe checkout session creation failed: {responseBody}");

        using var doc  = JsonDocument.Parse(responseBody);
        var root       = doc.RootElement;
        var sessionId  = root.TryGetProperty("id",             out var idEl)  ? idEl.GetString()  : null;
        var url        = root.TryGetProperty("url",            out var urlEl) ? urlEl.GetString() : null;
        var piId       = root.TryGetProperty("payment_intent", out var piEl)  ? piEl.GetString()  : null;

        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Stripe response missing session id/url.");

        return new StripeCheckoutSession(sessionId!, url!, piId);
    }

    // ── Webhook ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the Stripe-Signature header using HMAC-SHA256.
    /// Stripe signs the raw body with your webhook secret and sends:
    ///   Stripe-Signature: t=&lt;timestamp&gt;,v1=&lt;hex-signature&gt;
    /// </summary>
    public bool VerifyWebhookSignature(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_webhookSecret))
        {
            // No secret configured — skip verification (dev-only convenience)
            return true;
        }

        try
        {
            string? timestamp  = null;
            string? v1Sig      = null;

            foreach (var part in signatureHeader.Split(','))
            {
                if (part.StartsWith("t="))  timestamp = part[2..];
                if (part.StartsWith("v1=")) v1Sig     = part[3..];
            }

            if (timestamp is null || v1Sig is null) return false;

            var signedPayload  = $"{timestamp}.{rawBody}";
            var secretBytes    = Encoding.UTF8.GetBytes(_webhookSecret);
            var payloadBytes   = Encoding.UTF8.GetBytes(signedPayload);

            var computedHash   = HMACSHA256.HashData(secretBytes, payloadBytes);
            var computedHex    = Convert.ToHexString(computedHash).ToLowerInvariant();

            // Constant-time comparison to prevent timing attacks
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHex),
                Encoding.UTF8.GetBytes(v1Sig));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a raw Stripe event JSON body.
    /// Handles:
    ///   checkout.session.completed     → deposit succeeded
    ///   payment_intent.payment_failed  → deposit failed
    /// Returns null for any other event type.
    /// </summary>
    public StripeWebhookEvent? ParseWebhookEvent(string rawBody)
    {
        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        var eventType = root.TryGetProperty("type", out var typeEl)
            ? typeEl.GetString() : null;

        if (eventType is not ("checkout.session.completed" or "payment_intent.payment_failed"))
            return null;

        if (!root.TryGetProperty("data",   out var data) ||
            !data.TryGetProperty("object", out var obj))
            return null;

        if (eventType == "checkout.session.completed")
        {
            // obj is the Checkout Session
            var sessionId = obj.TryGetProperty("id", out var sidEl)
                ? sidEl.GetString() : null;

            var paymentIntentId = obj.TryGetProperty("payment_intent", out var piEl)
                ? piEl.GetString() : null;

            var paymentStatus = obj.TryGetProperty("payment_status", out var psEl)
                ? psEl.GetString() : null;

            // amount_total is in minor units (cents)
            decimal? amountTotal = null;
            if (obj.TryGetProperty("amount_total", out var amtEl) &&
                amtEl.TryGetDecimal(out var amtMinor))
            {
                amountTotal = amtMinor / 100m;
            }

            var currency = obj.TryGetProperty("currency", out var currEl)
                ? currEl.GetString()?.ToUpperInvariant() : null;

            // userId stored in client_reference_id when session was created
            string? userId = null;
            if (obj.TryGetProperty("client_reference_id", out var refEl))
                userId = refEl.GetString();

            // Also check metadata as a fallback
            if (string.IsNullOrWhiteSpace(userId) &&
                obj.TryGetProperty("metadata", out var meta) &&
                meta.TryGetProperty("userId", out var userIdEl))
            {
                userId = userIdEl.GetString();
            }

            return new StripeWebhookEvent(
                eventType!,
                SessionId:           sessionId,
                PaymentIntentId:     paymentIntentId,
                PaymentIntentStatus: paymentStatus,
                AmountReceived:      amountTotal,
                Currency:            currency,
                UserId:              userId);
        }
        else // payment_intent.payment_failed
        {
            var paymentIntentId = obj.TryGetProperty("id", out var piId)
                ? piId.GetString() : null;

            var piStatus = obj.TryGetProperty("status", out var piSt)
                ? piSt.GetString() : null;

            string? userId = null;
            if (obj.TryGetProperty("metadata", out var meta) &&
                meta.TryGetProperty("userId", out var userIdEl))
            {
                userId = userIdEl.GetString();
            }

            return new StripeWebhookEvent(
                eventType!,
                SessionId:           null,
                PaymentIntentId:     paymentIntentId,
                PaymentIntentStatus: piStatus,
                AmountReceived:      null,
                Currency:            null,
                UserId:              userId);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
            throw new InvalidOperationException("Stripe:SecretKey is not configured.");
    }

    private static long ToMinorUnits(decimal amount, string currency)
    {
        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        return (long)(rounded * 100m);
    }
}
