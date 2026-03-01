using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace StockX.Infrastructure.External.StripeApi;

public sealed class StripePaymentService : IStripeService
{
    private const string StripeApiBaseUrl = "https://api.stripe.com";

    private readonly HttpClient _httpClient;
    private readonly string _secretKey;

    public StripePaymentService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        _secretKey = configuration["Stripe:SecretKey"] ??
                     configuration["STRIPE_SECRET_KEY"] ??
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

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            throw new InvalidOperationException("STRIPE_SECRET_KEY is not configured.");
        }
    }

    private static long ToMinorUnits(decimal amount, string currency)
    {
        // MVP is USD-centric; keep a simple conversion (2 decimals).
        // Extend for zero-decimal currencies later if needed.
        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        return (long)(rounded * 100m);
    }
}

