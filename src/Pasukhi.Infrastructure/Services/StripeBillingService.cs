using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pasukhi.Application.DTOs.Billing;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Plans;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;
using Stripe;
using Stripe.Checkout;

namespace Pasukhi.Infrastructure.Services;

public class StripeBillingService : IBillingService
{
    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _config;
    private readonly ILogger<StripeBillingService> _logger;

    // price_id → SubscriptionTier reverse map (built at construction time)
    private readonly IReadOnlyDictionary<string, SubscriptionTier> _priceToTier;

    public StripeBillingService(
        PasukhiDbContext db,
        ITenantProvider tenantProvider,
        IConfiguration config,
        ILogger<StripeBillingService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _config = config;
        _logger = logger;

        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];

        var map = new Dictionary<string, SubscriptionTier>(StringComparer.OrdinalIgnoreCase);
        foreach (var tier in Enum.GetValues<SubscriptionTier>())
        {
            var priceId = config[$"Stripe:Prices:{tier}"];
            if (!string.IsNullOrWhiteSpace(priceId))
                map[priceId] = tier;
        }
        _priceToTier = map;
    }

    public async Task<CheckoutSessionDto> CreateCheckoutSessionAsync(string tierName, CancellationToken ct = default)
    {
        if (!Enum.TryParse<SubscriptionTier>(tierName, ignoreCase: true, out var tier))
            throw new InvalidOperationException($"Unknown subscription tier: {tierName}");

        var priceId = _config[$"Stripe:Prices:{tier}"];
        if (string.IsNullOrWhiteSpace(priceId))
            throw new InvalidOperationException($"No Stripe price configured for tier: {tier}");

        var businessId = EnsureTenant();
        var business = await _db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, ct)
            ?? throw new KeyNotFoundException("Business not found.");

        // Lazily create the Stripe Customer so we can correlate webhooks back to the business.
        var customerId = business.StripeCustomerId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            var customerService = new CustomerService();
            var customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Name = business.Name,
                Metadata = new Dictionary<string, string> { ["businessId"] = businessId.ToString() }
            }, cancellationToken: ct);
            business.StripeCustomerId = customer.Id;
            await _db.SaveChangesAsync(ct);
            customerId = customer.Id;
        }

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            LineItems =
            [
                new SessionLineItemOptions { Price = priceId, Quantity = 1 }
            ],
            SuccessUrl = _config["Stripe:CheckoutSuccessUrl"] ?? "http://localhost:5173/billing?success=1",
            CancelUrl = _config["Stripe:CheckoutCancelUrl"] ?? "http://localhost:5173/billing",
        }, cancellationToken: ct);

        return new CheckoutSessionDto(session.Url);
    }

    public async Task<PortalSessionDto> CreatePortalSessionAsync(CancellationToken ct = default)
    {
        var businessId = EnsureTenant();
        var business = await _db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, ct)
            ?? throw new KeyNotFoundException("Business not found.");

        if (string.IsNullOrWhiteSpace(business.StripeCustomerId))
            throw new InvalidOperationException("No Stripe customer found. Subscribe to a plan first.");

        var portalService = new Stripe.BillingPortal.SessionService();
        var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = business.StripeCustomerId,
            ReturnUrl = _config["Stripe:CustomerPortalReturnUrl"] ?? "http://localhost:5173/billing",
        }, cancellationToken: ct);

        return new PortalSessionDto(session.Url);
    }

    public async Task<BillingStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var businessId = EnsureTenant();
        var business = await _db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, ct)
            ?? throw new KeyNotFoundException("Business not found.");

        var limits = PlanLimits.ByTier[business.Tier];
        return new BillingStatusDto(
            business.Tier,
            business.SubscriptionStatus,
            business.CurrentPeriodEnd,
            !string.IsNullOrWhiteSpace(business.StripeSubscriptionId),
            limits);
    }

    public async Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken ct = default)
    {
        var webhookSecret = _config["Stripe:WebhookSecret"];
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed.");
            throw new InvalidOperationException("Invalid Stripe webhook signature.");
        }

        _logger.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

        switch (stripeEvent.Type)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
                await HandleSubscriptionUpsertAsync(stripeEvent, ct);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(stripeEvent, ct);
                break;

            case "invoice.payment_failed":
                await HandlePaymentFailedAsync(stripeEvent, ct);
                break;

            default:
                _logger.LogDebug("Stripe webhook event type {EventType} not handled.", stripeEvent.Type);
                break;
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task HandleSubscriptionUpsertAsync(Event stripeEvent, CancellationToken ct)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription is null) return;

        var business = await FindBusinessByCustomerIdAsync(subscription.CustomerId, ct);
        if (business is null) return;

        var priceId = subscription.Items.Data.FirstOrDefault()?.Price?.Id;
        if (priceId is null || !_priceToTier.TryGetValue(priceId, out var tier))
        {
            _logger.LogWarning("Stripe subscription {SubscriptionId} has unknown price {PriceId}.", subscription.Id, priceId);
            return;
        }

        var status = MapStatus(subscription.Status);
        business.Tier = tier;
        business.SubscriptionStatus = status;
        business.StripeSubscriptionId = subscription.Id;
        business.CurrentPeriodEnd = subscription.CurrentPeriodEnd;

        // Keep BusinessPrompt token cap in sync with the new tier.
        await SyncAiTokenCapAsync(business.Id, tier, ct);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Business {BusinessId} updated to tier {Tier} status {Status}.",
            business.Id, tier, status);
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent, CancellationToken ct)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription is null) return;

        var business = await FindBusinessByCustomerIdAsync(subscription.CustomerId, ct);
        if (business is null) return;

        business.Tier = SubscriptionTier.Free;
        business.SubscriptionStatus = SubscriptionStatus.Canceled;
        business.StripeSubscriptionId = null;
        business.CurrentPeriodEnd = null;

        await SyncAiTokenCapAsync(business.Id, SubscriptionTier.Free, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Business {BusinessId} subscription canceled — downgraded to Free.", business.Id);
    }

    private async Task HandlePaymentFailedAsync(Event stripeEvent, CancellationToken ct)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice?.CustomerId is null) return;

        var business = await FindBusinessByCustomerIdAsync(invoice.CustomerId, ct);
        if (business is null) return;

        business.SubscriptionStatus = SubscriptionStatus.PastDue;
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Business {BusinessId} payment failed — status set to PastDue.", business.Id);
    }

    private async Task SyncAiTokenCapAsync(Guid businessId, SubscriptionTier tier, CancellationToken ct)
    {
        var prompt = await _db.BusinessPrompts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.BusinessId == businessId, ct);

        if (prompt is null) return;

        var cap = PlanLimits.ByTier[tier].MaxAiTokensPerDay;
        prompt.MaxAiTokensPerDay = cap == PlanLimits.Unlimited ? int.MaxValue : cap;
        prompt.IsAiEnabled = PlanLimits.ByTier[tier].AiEnabled;
    }

    private async Task<Pasukhi.Domain.Entities.Business?> FindBusinessByCustomerIdAsync(
        string customerId, CancellationToken ct) =>
        await _db.Businesses.FirstOrDefaultAsync(b => b.StripeCustomerId == customerId, ct);

    private static SubscriptionStatus MapStatus(string stripeStatus) => stripeStatus switch
    {
        "active" => SubscriptionStatus.Active,
        "past_due" => SubscriptionStatus.PastDue,
        "canceled" => SubscriptionStatus.Canceled,
        "incomplete" or "incomplete_expired" => SubscriptionStatus.Incomplete,
        "trialing" => SubscriptionStatus.Trialing,
        _ => SubscriptionStatus.Active
    };

    private Guid EnsureTenant()
    {
        if (_tenantProvider.BusinessId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required.");
        return _tenantProvider.BusinessId;
    }
}
