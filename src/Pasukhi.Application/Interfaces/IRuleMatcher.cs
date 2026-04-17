using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Interfaces;

public record RuleMatchResult(AutomationRule Rule, double Score);

public interface IRuleMatcher
{
    Task<IReadOnlyList<RuleMatchResult>> FindMatchesAsync(
        Guid businessId,
        string messageText,
        MessageType messageType,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default);
}
