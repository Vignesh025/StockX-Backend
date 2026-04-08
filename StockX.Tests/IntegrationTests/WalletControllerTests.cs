using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Infrastructure.External.StripeApi;
using StockX.Infrastructure.Persistence.Context;
using Xunit;

namespace StockX.Tests.IntegrationTests;

public sealed class WalletControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WalletControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/wallet/balance ────────────────────────────────────────────────

    [Fact]
    public async Task GetBalance_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/wallet/balance");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBalance_NoTransactions_Returns200WithZeroBalance()
    {
        var user = _factory.SeedUser(email: $"wb_zero_{Guid.NewGuid()}@example.com");
        var client = _factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/wallet/balance");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"balance\":0");
    }

    [Fact]
    public async Task GetBalance_WithDeposit_Returns200WithCorrectBalance()
    {
        var user = _factory.SeedUser(email: $"wb_dep_{Guid.NewGuid()}@example.com");
        SeedDeposit(user.UserId, 750m);

        var client = _factory.CreateAuthenticatedClient(user);
        var response = await client.GetAsync("/api/wallet/balance");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("750");
    }

    // ── POST /api/wallet/deposit/initiate ──────────────────────────────────────

    [Fact]
    public async Task InitiateDeposit_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/wallet/deposit/initiate",
            new { amount = 100 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InitiateDeposit_ZeroAmount_Returns500()
    {
        var user = _factory.SeedUser(email: $"dep_zero_{Guid.NewGuid()}@example.com");
        var client = _factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/wallet/deposit/initiate",
            new { amount = 0 });

        // ArgumentOutOfRangeException → ExceptionHandlingMiddleware → 500
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task InitiateDeposit_ValidAmount_Returns200WithCheckoutUrl()
    {
        var user = _factory.SeedUser(email: $"dep_ok_{Guid.NewGuid()}@example.com");

        _factory.StripeMock
            .Setup(s => s.CreateDepositCheckoutSessionAsync(
                user.UserId, 250m, "usd",
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeCheckoutSession(
                "sess_test_123",
                "https://checkout.stripe.com/pay/sess_test_123",
                "pi_test_abc"));

        var client = _factory.CreateAuthenticatedClient(user);
        var response = await client.PostAsJsonAsync("/api/wallet/deposit/initiate",
            new { amount = 250 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("checkoutUrl");
        json.Should().Contain("https://checkout.stripe.com/pay/sess_test_123");
        json.Should().Contain("paymentIntentId");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void SeedDeposit(Guid userId, decimal amount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Transactions.Add(new Transaction
        {
            TransactionId = Guid.NewGuid(),
            UserId = userId,
            Type = TransactionType.Deposit,
            Amount = amount,
            Status = TransactionStatus.Completed,
            Timestamp = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}
