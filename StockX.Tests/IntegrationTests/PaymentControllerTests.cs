using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Infrastructure.External.StripeApi;
using StockX.Infrastructure.Persistence.Context;
using Xunit;

namespace StockX.Tests.IntegrationTests;

public sealed class PaymentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PaymentControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── POST /api/payment/webhook/stripe ──────────────────────────────────────

    [Fact]
    public async Task StripeWebhook_MissingSignatureHeader_Returns400()
    {
        var client = _factory.CreateClient();
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/payment/webhook/stripe", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Missing Stripe-Signature header");
    }

    [Fact]
    public async Task StripeWebhook_InvalidSignature_Returns400()
    {
        _factory.StripeMock
            .Setup(s => s.VerifyWebhookSignature(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment/webhook/stripe")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "invalid_sig");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid signature");
    }

    [Fact]
    public async Task StripeWebhook_ValidSignature_UnhandledEventType_Returns200()
    {
        _factory.StripeMock
            .Setup(s => s.VerifyWebhookSignature(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        _factory.StripeMock
            .Setup(s => s.ParseWebhookEvent(It.IsAny<string>()))
            .Returns((StripeWebhookEvent?)null);   // unhandled event type

        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment/webhook/stripe")
        {
            Content = new StringContent("{\"type\":\"customer.created\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "valid_sig");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StripeWebhook_CheckoutCompleted_NoIntentFound_Returns200WithoutCrash()
    {
        // Arrange — valid signature, parsed event, but no matching PaymentIntent in DB
        _factory.StripeMock
            .Setup(s => s.VerifyWebhookSignature(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        _factory.StripeMock
            .Setup(s => s.ParseWebhookEvent(It.IsAny<string>()))
            .Returns(new StripeWebhookEvent(
                "checkout.session.completed",
                "sess_missing",
                "pi_abc",
                "succeeded",
                100m,
                "usd",
                Guid.NewGuid().ToString()));

        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment/webhook/stripe")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "valid_sig");

        // Act
        var response = await client.SendAsync(request);

        // Assert — should still return 200 (intent just not found → logged + skipped)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StripeWebhook_CheckoutCompleted_ExistingIntent_CreditsWallet()
    {
        // Arrange
        var user = _factory.SeedUser(email: $"webhook_ok_{Guid.NewGuid()}@example.com");
        const string sessionId = "sess_real_test_ok";
        SeedPaymentIntent(user.UserId, sessionId, 500m);

        _factory.StripeMock
            .Setup(s => s.VerifyWebhookSignature(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        _factory.StripeMock
            .Setup(s => s.ParseWebhookEvent(It.IsAny<string>()))
            .Returns(new StripeWebhookEvent(
                "checkout.session.completed",
                sessionId,
                "pi_real",
                "succeeded",
                500m,
                "usd",
                user.UserId.ToString()));

        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment/webhook/stripe")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "valid_sig");

        // Act
        var response = await client.SendAsync(request);

        // Assert — 200 and a deposit transaction must exist
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deposit = db.Transactions.FirstOrDefault(t =>
            t.UserId == user.UserId && t.Type == TransactionType.Deposit);
        deposit.Should().NotBeNull();
        deposit!.Amount.Should().Be(500m);
    }

    [Fact]
    public async Task StripeWebhook_CheckoutCompleted_DuplicateEvent_IgnoresDuplicate()
    {
        // Arrange — intent already Completed
        var user = _factory.SeedUser(email: $"webhook_dup_{Guid.NewGuid()}@example.com");
        const string sessionId = "sess_completed_already";
        SeedPaymentIntent(user.UserId, sessionId, 200m, alreadyCompleted: true);

        _factory.StripeMock
            .Setup(s => s.VerifyWebhookSignature(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        _factory.StripeMock
            .Setup(s => s.ParseWebhookEvent(It.IsAny<string>()))
            .Returns(new StripeWebhookEvent(
                "checkout.session.completed",
                sessionId,
                "pi_dup",
                "succeeded",
                200m,
                "usd",
                user.UserId.ToString()));

        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment/webhook/stripe")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "valid_sig");

        // Act
        var response = await client.SendAsync(request);

        // Assert — 200 but no new deposit created (duplicate suppressed)
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deposits = db.Transactions
            .Where(t => t.UserId == user.UserId && t.Type == TransactionType.Deposit)
            .ToList();
        deposits.Should().BeEmpty();   // no deposit was inserted
    }

    [Fact]
    public async Task StripeWebhook_PaymentFailed_ExistingPendingIntent_SetsStatusFailed()
    {
        // Arrange
        var user = _factory.SeedUser(email: $"webhook_fail_{Guid.NewGuid()}@example.com");
        const string sessionId = "sess_fail_pi";
        SeedPaymentIntent(user.UserId, sessionId, 300m);

        _factory.StripeMock
            .Setup(s => s.VerifyWebhookSignature(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        _factory.StripeMock
            .Setup(s => s.ParseWebhookEvent(It.IsAny<string>()))
            .Returns(new StripeWebhookEvent(
                "payment_intent.payment_failed",
                "sess_any",
                sessionId,          // PaymentIntentId used for lookup
                "failed",
                null, null,
                user.UserId.ToString()));

        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment/webhook/stripe")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "valid_sig");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var intent = db.PaymentIntents.Find(sessionId);
        intent!.Status.Should().Be(PaymentIntentStatus.Failed);
    }

    [Fact]
    public async Task StripeWebhook_CheckoutCompleted_MissingSessionId_Returns200WithoutCrash()
    {
        _factory.StripeMock
            .Setup(s => s.VerifyWebhookSignature(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        _factory.StripeMock
            .Setup(s => s.ParseWebhookEvent(It.IsAny<string>()))
            .Returns(new StripeWebhookEvent(
                "checkout.session.completed",
                null,   // no session ID
                "pi_abc",
                "succeeded",
                100m, "usd",
                null));

        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment/webhook/stripe")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "valid_sig");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void SeedPaymentIntent(
        Guid userId,
        string intentId,
        decimal amount,
        bool alreadyCompleted = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.PaymentIntents.Add(new PaymentIntent
        {
            IntentId = intentId,
            UserId = userId,
            Amount = amount,
            Currency = "USD",
            Status = alreadyCompleted ? PaymentIntentStatus.Completed : PaymentIntentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = alreadyCompleted ? DateTime.UtcNow : null
        });
        db.SaveChanges();
    }
}
