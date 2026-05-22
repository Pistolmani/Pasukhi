using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.Exceptions;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Plans;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class PlanLimitsService : IPlanLimitsService
{
    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public PlanLimitsService(PasukhiDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task<PlanLimitDefinition> GetCurrentAsync(CancellationToken ct = default)
    {
        var tier = await GetTierAsync(ct);
        return PlanLimits.ByTier[tier];
    }

    public async Task EnsureCanAddChannelAsync(CancellationToken ct = default)
    {
        var tier = await GetTierAsync(ct);
        var limits = PlanLimits.ByTier[tier];
        if (limits.MaxChannels == PlanLimits.Unlimited) return;

        var count = await _db.ChannelConnections.CountAsync(ct);
        if (count >= limits.MaxChannels)
        {
            throw new PlanLimitExceededException(
                resource: "channels",
                currentTier: tier,
                limit: limits.MaxChannels,
                suggestedTier: PlanLimits.SuggestedUpgradeFor(tier, l => l.MaxChannels > limits.MaxChannels));
        }
    }

    public async Task EnsureCanAddFaqAsync(CancellationToken ct = default)
    {
        var tier = await GetTierAsync(ct);
        var limits = PlanLimits.ByTier[tier];
        if (limits.MaxFaqs == PlanLimits.Unlimited) return;

        var count = await _db.FaqItems.CountAsync(ct);
        if (count >= limits.MaxFaqs)
        {
            throw new PlanLimitExceededException(
                resource: "faqs",
                currentTier: tier,
                limit: limits.MaxFaqs,
                suggestedTier: PlanLimits.SuggestedUpgradeFor(tier, l => l.MaxFaqs > limits.MaxFaqs));
        }
    }

    public async Task EnsureCanAddRuleAsync(CancellationToken ct = default)
    {
        var tier = await GetTierAsync(ct);
        var limits = PlanLimits.ByTier[tier];
        if (limits.MaxRules == PlanLimits.Unlimited) return;

        var count = await _db.AutomationRules.CountAsync(ct);
        if (count >= limits.MaxRules)
        {
            throw new PlanLimitExceededException(
                resource: "rules",
                currentTier: tier,
                limit: limits.MaxRules,
                suggestedTier: PlanLimits.SuggestedUpgradeFor(tier, l => l.MaxRules > limits.MaxRules));
        }
    }

    public async Task<bool> IsAiEnabledAsync(CancellationToken ct = default)
    {
        var limits = await GetCurrentAsync(ct);
        return limits.AiEnabled;
    }

    public async Task<bool> CanSyncMessengerProfileAsync(CancellationToken ct = default)
    {
        var limits = await GetCurrentAsync(ct);
        return limits.MessengerProfileSync;
    }

    private async Task<SubscriptionTier> GetTierAsync(CancellationToken ct)
    {
        var businessId = _tenantProvider.BusinessId;
        if (businessId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required.");

        return await _db.Businesses
            .Where(b => b.Id == businessId)
            .Select(b => b.Tier)
            .FirstOrDefaultAsync(ct);
    }
}
