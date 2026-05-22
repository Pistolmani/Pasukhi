using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Billing;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billing;
    private readonly ILogger<BillingController> _logger;

    public BillingController(IBillingService billing, ILogger<BillingController> logger)
    {
        _billing = billing;
        _logger = logger;
    }

    /// <summary>Creates a Stripe Checkout Session for the requested tier.</summary>
    [Authorize]
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CreateCheckoutSessionRequest request,
        CancellationToken ct)
    {
        var session = await _billing.CreateCheckoutSessionAsync(request.Tier, ct);
        return Ok(session);
    }

    /// <summary>Creates a Stripe Billing Portal session for the current business.</summary>
    [Authorize]
    [HttpPost("portal")]
    public async Task<IActionResult> CreatePortal(CancellationToken ct)
    {
        var session = await _billing.CreatePortalSessionAsync(ct);
        return Ok(session);
    }

    /// <summary>Returns the current subscription tier and limits for the business.</summary>
    [Authorize]
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var status = await _billing.GetStatusAsync(ct);
        return Ok(status);
    }

    /// <summary>
    /// Receives Stripe webhook events. Must remain unauthenticated — Stripe
    /// signs the payload with the webhook secret instead of using JWT.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

        await _billing.HandleWebhookAsync(payload, signature, ct);
        return Ok();
    }
}
