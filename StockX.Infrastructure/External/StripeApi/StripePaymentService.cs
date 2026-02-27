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
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);
        }
    }

    public async Task<StripeCheckoutSession> CreateDepositCheckoutSessionAsync(
        Guid userId,
        decimal amount,
        string currency,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        if (string.IsNullOrWhiteSpace(successUrl))
        {
            throw new ArgumentException("Success URL is required.", nameof(successUrl));
        }

        if (string.IsNullOrWhiteSpace(cancelUrl))
        {
            throw new ArgumentException("Cancel URL is required.", nameof(cancelUrl));
        }

        EnsureConfigured();

        var unitAmount = ToMinorUnits(amount, currency);

        var form = new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["success_url"] = successUrl,
            ["cancel_url"] = cancelUrl,
            ["client_reference_id"] = userId.ToString(),
            ["metadata[userId]"] = userId.ToString(),

            ["line_items[0][price_data][currency]"] = currency.ToLowerInvariant(),
            ["line_items[0][price_data][product_data][name]"] = "Wallet Deposit",
            ["line_items[0][price_data][unit_amount]"] = unitAmount.ToString(CultureInfo.InvariantCulture),
            ["line_items[0][quantity]"] = "1",

            ["payment_intent_data[metadata][userId]"] = userId.ToString()
        };

        using var content = new FormUrlEncodedContent(form);

        using var response = await _httpClient.PostAsync(
            $"{StripeApiBaseUrl}/v1/checkout/sessions",
            content,
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Stripe checkout session creation failed: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var sessionId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var url = root.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
        var paymentIntentId = root.TryGetProperty("payment_intent", out var piEl) ? piEl.GetString() : null;

        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Stripe response missing session id/url.");
        }

        return new StripeCheckoutSession(sessionId!, url!, paymentIntentId);
    }

    public void VerifyWebhookSignature(
        string payload,
        string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload is required.", nameof(payload));
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            throw new ArgumentException("Stripe-Signature header is required.", nameof(signatureHeader));
        }

        EnsureConfigured();

        var parsed = ParseStripeSignatureHeader(signatureHeader);
        var signedPayload = $"{parsed.Timestamp}.{payload}";

        var expected = ComputeHmacSha256Hex(_webhookSecret, signedPayload);

        var valid = parsed.SignaturesV1.Any(sig => FixedTimeEqualsHex(sig, expected));

        if (!valid)
        {
            throw new InvalidOperationException("Invalid Stripe webhook signature.");
        }
    }

    public StripeWebhookEvent ParseWebhookEvent(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var eventId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;

        string? paymentIntentId = null;

        if (root.TryGetProperty("data", out var dataEl) &&
            dataEl.TryGetProperty("object", out var objEl))
        {
            paymentIntentId =
                objEl.TryGetProperty("payment_intent", out var piEl) ? piEl.GetString() :
                objEl.TryGetProperty("id", out var objIdEl) ? objIdEl.GetString() :
                null;
        }

        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(type))
        {
            throw new InvalidOperationException("Stripe webhook payload missing id/type.");
        }

        return new StripeWebhookEvent(eventId!, type!, paymentIntentId);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            throw new InvalidOperationException("STRIPE_SECRET_KEY is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_webhookSecret))
        {
            throw new InvalidOperationException("STRIPE_WEBHOOK_SECRET is not configured.");
        }
    }

    private static long ToMinorUnits(decimal amount, string currency)
    {
        // MVP is USD-centric; keep a simple conversion (2 decimals).
        // Extend for zero-decimal currencies later if needed.
        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        return (long)(rounded * 100m);
    }

    private static (long Timestamp, IReadOnlyList<string> SignaturesV1) ParseStripeSignatureHeader(string header)
    {
        // Stripe header format: "t=1492774577,v1=5257a869e7...,v0=..."
        long? timestamp = null;
        var v1 = new List<string>();

        foreach (var part in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = part.IndexOf('=', StringComparison.Ordinal);
            if (idx <= 0) continue;

            var key = part[..idx];
            var value = part[(idx + 1)..];

            if (key == "t" && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t))
            {
                timestamp = t;
            }
            else if (key == "v1")
            {
                v1.Add(value);
            }
        }

        if (timestamp is null || v1.Count == 0)
        {
            throw new InvalidOperationException("Malformed Stripe-Signature header.");
        }

        return (timestamp.Value, v1);
    }

    private static string ComputeHmacSha256Hex(string secret, string message)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var msgBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(msgBytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEqualsHex(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}

