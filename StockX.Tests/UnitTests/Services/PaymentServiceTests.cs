using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using StockX.Core.Entities;
using StockX.Core.Enums;
using StockX.Core.Interfaces.Persistence;
using StockX.Core.Interfaces.Repositories;
using StockX.Infrastructure.External.StripeApi;
using StockX.Services.Payment;
using Xunit;

namespace StockX.Tests.UnitTests.Services;

public sealed class PaymentServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentIntentRepository> _paymentIntentRepoMock;
    private readonly Mock<IStripeService> _stripeServiceMock;
    private readonly Mock<IRepository<PaymentIntent>> _paymentIntentsRepoMock;
    private readonly IConfiguration _configuration;
    private readonly PaymentService _sut;

    public PaymentServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _paymentIntentRepoMock = new Mock<IPaymentIntentRepository>();
        _stripeServiceMock = new Mock<IStripeService>();
        _paymentIntentsRepoMock = new Mock<IRepository<PaymentIntent>>();

        _unitOfWorkMock.Setup(u => u.PaymentIntents).Returns(_paymentIntentsRepoMock.Object);

        var configValues = new Dictionary<string, string?>
        {
            ["Stripe:SuccessUrl"] = "https://example.com/success",
            ["Stripe:CancelUrl"] = "https://example.com/cancel"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _sut = new PaymentService(
            _unitOfWorkMock.Object,
            _paymentIntentRepoMock.Object,
            _stripeServiceMock.Object,
            _configuration);
    }

    // ── InitiateDepositAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task InitiateDepositAsync_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        var act = () => _sut.InitiateDepositAsync(Guid.NewGuid(), -1m);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task InitiateDepositAsync_ZeroAmount_ThrowsArgumentOutOfRangeException()
    {
        var act = () => _sut.InitiateDepositAsync(Guid.NewGuid(), 0m);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task InitiateDepositAsync_ValidAmount_CreatesSessionStoresIntentAndReturnsResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const decimal amount = 500m;
        var session = new StripeCheckoutSession("sess_123", "https://checkout.stripe.com/pay/sess_123", "pi_abc");

        _stripeServiceMock
            .Setup(s => s.CreateDepositCheckoutSessionAsync(
                userId, amount, "usd",
                "https://example.com/success",
                "https://example.com/cancel",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _paymentIntentsRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PaymentIntent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.InitiateDepositAsync(userId, amount);

        // Assert
        result.CheckoutUrl.Should().Be("https://checkout.stripe.com/pay/sess_123");
        result.PaymentIntentId.Should().Be("pi_abc");
        result.Amount.Should().Be(500m);
        result.Currency.Should().Be("USD");

        _paymentIntentsRepoMock.Verify(r => r.AddAsync(It.IsAny<PaymentIntent>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitiateDepositAsync_SessionHasNoPaymentIntentId_FallsBackToSessionId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        // PaymentIntentId is null → should use SessionId as the IntentId
        var session = new StripeCheckoutSession("sess_fallback", "https://checkout.stripe.com/pay/sess_fallback", null);

        _stripeServiceMock
            .Setup(s => s.CreateDepositCheckoutSessionAsync(
                userId, 100m, "usd",
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _paymentIntentsRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PaymentIntent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.InitiateDepositAsync(userId, 100m);

        // Assert
        result.PaymentIntentId.Should().Be("sess_fallback");
    }

    // ── GetPaymentIntentAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetPaymentIntentAsync_ExistingIntent_ReturnsIntent()
    {
        // Arrange
        var intent = new PaymentIntent { IntentId = "pi_abc", Amount = 100m };

        _paymentIntentRepoMock
            .Setup(r => r.GetByIntentIdAsync("pi_abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(intent);

        // Act
        var result = await _sut.GetPaymentIntentAsync("pi_abc");

        // Assert
        result.Should().Be(intent);
    }

    [Fact]
    public async Task GetPaymentIntentAsync_NotFound_ReturnsNull()
    {
        // Arrange
        _paymentIntentRepoMock
            .Setup(r => r.GetByIntentIdAsync("pi_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentIntent?)null);

        // Act
        var result = await _sut.GetPaymentIntentAsync("pi_missing");

        // Assert
        result.Should().BeNull();
    }

    // ── UpdatePaymentIntentStatusAsync ─────────────────────────────────────────

    [Fact]
    public async Task UpdatePaymentIntentStatusAsync_ExistingIntent_UpdatesAndSaves()
    {
        // Arrange
        var intent = new PaymentIntent { IntentId = "pi_abc", Status = PaymentIntentStatus.Pending };
        var transactionId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;

        _paymentIntentRepoMock
            .Setup(r => r.GetByIntentIdAsync("pi_abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(intent);

        _paymentIntentsRepoMock
            .Setup(r => r.Update(intent));

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _sut.UpdatePaymentIntentStatusAsync("pi_abc", PaymentIntentStatus.Completed, transactionId, completedAt);

        // Assert
        intent.Status.Should().Be(PaymentIntentStatus.Completed);
        intent.TransactionId.Should().Be(transactionId);
        intent.CompletedAt.Should().Be(completedAt);
        _paymentIntentsRepoMock.Verify(r => r.Update(intent), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePaymentIntentStatusAsync_IntentNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        _paymentIntentRepoMock
            .Setup(r => r.GetByIntentIdAsync("pi_missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentIntent?)null);

        // Act
        var act = () => _sut.UpdatePaymentIntentStatusAsync(
            "pi_missing", PaymentIntentStatus.Completed, null, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Payment intent not found.");
    }
}
