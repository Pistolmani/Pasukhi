using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Text;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class RuleMatcher : IRuleMatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private readonly PasukhiDbContext _db;

    public RuleMatcher(PasukhiDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RuleMatchResult>> FindMatchesAsync(
        Guid businessId,
        string messageText,
        MessageType messageType,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        if (businessId == Guid.Empty)
        {
            return Array.Empty<RuleMatchResult>();
        }

        // Matchers are called by background consumers too, so they use explicit businessId
        // predicates instead of depending on whichever tenant provider is currently active.
        var rules = await _db.AutomationRules
            .IgnoreQueryFilters()
            .Where(r => r.BusinessId == businessId && r.IsActive)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var matches = new List<RuleMatchResult>();

        foreach (var rule in rules)
        {
            var score = ScoreRule(rule, messageText, messageType, receivedAt);
            if (score <= 0)
            {
                continue;
            }

            rule.MatchCount++;
            matches.Add(new RuleMatchResult(rule, score));
        }

        if (matches.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return matches;
    }

    private static double ScoreRule(
        AutomationRule rule,
        string messageText,
        MessageType messageType,
        DateTimeOffset receivedAt) =>
        rule.TriggerType switch
        {
            TriggerType.Keyword => ScoreKeyword(rule.TriggerValue, messageText),
            TriggerType.Regex => MatchesRegex(rule.TriggerValue, messageText) ? 1.0 : 0.0,
            TriggerType.MessageType => MatchesMessageType(rule.TriggerValue, messageType) ? 1.0 : 0.0,
            TriggerType.TimeOfDay => MatchesTimeOfDay(rule.TriggerValue, receivedAt) ? 1.0 : 0.0,
            _ => 0.0
        };

    private static double ScoreKeyword(string triggerValue, string messageText)
    {
        var normalizedMessage = TextNormalizer.Normalize(messageText);
        var values = TextNormalizer.SplitCsv(triggerValue)
            .Select(TextNormalizer.Normalize)
            .Where(value => value.Length > 0)
            .ToList();

        if (values.Count == 0)
        {
            return 0.0;
        }

        var matches = values.Count(normalizedMessage.Contains);
        return matches == 0 ? 0.0 : (double)matches / values.Count;
    }

    private static bool MatchesRegex(string pattern, string messageText)
    {
        try
        {
            return Regex.IsMatch(
                messageText ?? string.Empty,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                RegexTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool MatchesMessageType(string triggerValue, MessageType messageType)
    {
        if (Enum.TryParse<MessageType>(triggerValue, ignoreCase: true, out var expected))
        {
            return expected == messageType;
        }

        return int.TryParse(triggerValue, out var value) && value == (int)messageType;
    }

    private static bool MatchesTimeOfDay(string triggerValue, DateTimeOffset receivedAt)
    {
        var parts = triggerValue.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !TimeSpan.TryParse(parts[0], out var start) || !TimeSpan.TryParse(parts[1], out var end))
        {
            return false;
        }

        var current = receivedAt.TimeOfDay;
        return start <= end
            ? current >= start && current <= end
            : current >= start || current <= end;
    }
}
