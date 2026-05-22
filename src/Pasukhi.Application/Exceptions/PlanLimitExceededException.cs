using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Exceptions;

public class PlanLimitExceededException : Exception
{
    public string Resource { get; }
    public SubscriptionTier CurrentTier { get; }
    public int Limit { get; }
    public SubscriptionTier SuggestedTier { get; }

    public PlanLimitExceededException(
        string resource,
        SubscriptionTier currentTier,
        int limit,
        SubscriptionTier suggestedTier)
        : base($"Plan limit exceeded for '{resource}': limit is {limit} on the {currentTier} plan.")
    {
        Resource = resource;
        CurrentTier = currentTier;
        Limit = limit;
        SuggestedTier = suggestedTier;
    }
}
