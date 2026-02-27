using Microsoft.AspNetCore.Mvc;
using StockX.Core.Services.Interfaces;

namespace StockX.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("webhook/stripe")]
    public async Task<IActionResult> StripeWebhook(
        [FromBody] string payload,
        [FromHeader(Name = "Stripe-Signature")] string signature,
        CancellationToken cancellationToken)
    {
        await _paymentService.HandleStripeWebhookAsync(
            payload,
            signature,
            cancellationToken);

        return Ok();
    }
}

