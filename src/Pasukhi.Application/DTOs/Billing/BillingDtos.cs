using Pasukhi.Application.Plans;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.DTOs.Billing;

public record BillingStatusDto(
    SubscriptionTier Tier,
    SubscriptionStatus SubscriptionStatus,
    DateTime? CurrentPeriodEnd,
    bool HasStripeSubscription,
    PlanLimitDefinition Limits);

public record CreateCheckoutSessionRequest(string Tier);

public record CheckoutSessionDto(string Url);

public record PortalSessionDto(string Url);
