using Pasukhi.Application.Plans;

namespace Pasukhi.Application.Interfaces;

public interface IPlanLimitsService
{
    Task<PlanLimitDefinition> GetCurrentAsync(CancellationToken ct = default);
    Task EnsureCanAddChannelAsync(CancellationToken ct = default);
    Task EnsureCanAddFaqAsync(CancellationToken ct = default);
    Task EnsureCanAddRuleAsync(CancellationToken ct = default);
    Task<bool> IsAiEnabledAsync(CancellationToken ct = default);
    Task<bool> CanSyncMessengerProfileAsync(CancellationToken ct = default);
}
