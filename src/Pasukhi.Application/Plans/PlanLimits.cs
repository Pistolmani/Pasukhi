using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Plans;

public record PlanLimitDefinition(
    int MaxChannels,
    int MaxFaqs,
    int MaxRules,
    bool AiEnabled,
    int MaxAiTokensPerDay,
    bool MessengerProfileSync,
    bool FullAnalytics,
    bool PrioritySupport);

public static class PlanLimits
{
    public const int Unlimited = int.MaxValue;

    public static readonly IReadOnlyDictionary<SubscriptionTier, PlanLimitDefinition> ByTier =
        new Dictionary<SubscriptionTier, PlanLimitDefinition>
        {
            [SubscriptionTier.Free] = new(
                MaxChannels: 1,
                MaxFaqs: 5,
                MaxRules: 3,
                AiEnabled: false,
                MaxAiTokensPerDay: 0,
                MessengerProfileSync: false,
                FullAnalytics: false,
                PrioritySupport: false),

            [SubscriptionTier.Starter] = new(
                MaxChannels: 2,
                MaxFaqs: 25,
                MaxRules: 10,
                AiEnabled: true,
                MaxAiTokensPerDay: 50_000,
                MessengerProfileSync: true,
                FullAnalytics: false,
                PrioritySupport: false),

            [SubscriptionTier.Pro] = new(
                MaxChannels: 3,
                MaxFaqs: Unlimited,
                MaxRules: Unlimited,
                AiEnabled: true,
                MaxAiTokensPerDay: 500_000,
                MessengerProfileSync: true,
                FullAnalytics: true,
                PrioritySupport: false),

            [SubscriptionTier.Agency] = new(
                MaxChannels: Unlimited,
                MaxFaqs: Unlimited,
                MaxRules: Unlimited,
                AiEnabled: true,
                MaxAiTokensPerDay: 2_000_000,
                MessengerProfileSync: true,
                FullAnalytics: true,
                PrioritySupport: true),

            [SubscriptionTier.Enterprise] = new(
                MaxChannels: Unlimited,
                MaxFaqs: Unlimited,
                MaxRules: Unlimited,
                AiEnabled: true,
                MaxAiTokensPerDay: Unlimited,
                MessengerProfileSync: true,
                FullAnalytics: true,
                PrioritySupport: true),
        };

    /// <summary>
    /// Returns the lowest tier above <paramref name="current"/> that satisfies
    /// <paramref name="hasCapacity"/>, falling back to Enterprise.
    /// </summary>
    public static SubscriptionTier SuggestedUpgradeFor(
        SubscriptionTier current,
        Func<PlanLimitDefinition, bool> hasCapacity)
    {
        var tiers = Enum.GetValues<SubscriptionTier>().OrderBy(t => (int)t);
        return tiers.FirstOrDefault(
            t => (int)t > (int)current && hasCapacity(ByTier[t]),
            SubscriptionTier.Enterprise);
    }
}
