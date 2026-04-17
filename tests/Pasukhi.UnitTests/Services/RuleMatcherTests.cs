using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Services;

namespace Pasukhi.UnitTests.Services;

public class RuleMatcherTests
{
    [Fact]
    public async Task Keyword_match_returns_score_and_increments_count()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var rule = Rule(businessId, "Price", 0, TriggerType.Keyword, "price, cost, ფასი");
        db.AutomationRules.Add(rule);
        await db.SaveChangesAsync();

        var result = await new RuleMatcher(db).FindMatchesAsync(
            businessId,
            "რა ფასია?",
            MessageType.Text,
            DateTimeOffset.Parse("2026-04-17T12:00:00+04:00"));

        Assert.Single(result);
        Assert.Equal(rule.Id, result[0].Rule.Id);
        Assert.True(result[0].Score > 0);
        Assert.Equal(1, rule.MatchCount);
    }

    [Fact]
    public async Task Regex_match_returns_full_score()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        db.AutomationRules.Add(Rule(businessId, "Order", 0, TriggerType.Regex, @"order\s+\d+"));
        await db.SaveChangesAsync();

        var result = await new RuleMatcher(db).FindMatchesAsync(
            businessId,
            "order 123",
            MessageType.Text,
            DateTimeOffset.UtcNow);

        Assert.Single(result);
        Assert.Equal(1.0, result[0].Score);
    }

    [Fact]
    public async Task Invalid_regex_returns_no_match_without_throwing()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        db.AutomationRules.Add(Rule(businessId, "Invalid", 0, TriggerType.Regex, "["));
        await db.SaveChangesAsync();

        var result = await new RuleMatcher(db).FindMatchesAsync(
            businessId,
            "anything",
            MessageType.Text,
            DateTimeOffset.UtcNow);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Catastrophic_regex_timeout_returns_no_match_without_throwing()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        db.AutomationRules.Add(Rule(businessId, "Timeout", 0, TriggerType.Regex, "^(a+)+$"));
        await db.SaveChangesAsync();

        var result = await new RuleMatcher(db).FindMatchesAsync(
            businessId,
            new string('a', 5000) + "!",
            MessageType.Text,
            DateTimeOffset.UtcNow);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("0")]
    public async Task Message_type_match_supports_name_and_number(string triggerValue)
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        db.AutomationRules.Add(Rule(businessId, "Text only", 0, TriggerType.MessageType, triggerValue));
        await db.SaveChangesAsync();

        var result = await new RuleMatcher(db).FindMatchesAsync(
            businessId,
            "hello",
            MessageType.Text,
            DateTimeOffset.UtcNow);

        Assert.Single(result);
        Assert.Equal(1.0, result[0].Score);
    }

    [Fact]
    public async Task Time_of_day_match_handles_midnight_wrap()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        db.AutomationRules.Add(Rule(businessId, "After hours", 0, TriggerType.TimeOfDay, "18:00-09:00"));
        await db.SaveChangesAsync();

        var result = await new RuleMatcher(db).FindMatchesAsync(
            businessId,
            "hello",
            MessageType.Text,
            DateTimeOffset.Parse("2026-04-17T23:30:00+04:00"));

        Assert.Single(result);
    }

    [Fact]
    public async Task Matches_are_returned_in_priority_order_and_inactive_rules_are_excluded()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var later = Rule(businessId, "Later", 5, TriggerType.Keyword, "help");
        var first = Rule(businessId, "First", 1, TriggerType.Keyword, "help");
        var inactive = Rule(businessId, "Inactive", 0, TriggerType.Keyword, "help");
        inactive.IsActive = false;

        db.AutomationRules.AddRange(later, first, inactive);
        await db.SaveChangesAsync();

        var result = await new RuleMatcher(db).FindMatchesAsync(
            businessId,
            "help please",
            MessageType.Text,
            DateTimeOffset.UtcNow);

        Assert.Equal(new[] { first.Id, later.Id }, result.Select(r => r.Rule.Id));
        Assert.Equal(1, first.MatchCount);
        Assert.Equal(1, later.MatchCount);
        Assert.Equal(0, inactive.MatchCount);
    }

    private static AutomationRule Rule(
        Guid businessId,
        string name,
        int priority,
        TriggerType triggerType,
        string triggerValue) =>
        new()
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = name,
            Priority = priority,
            TriggerType = triggerType,
            TriggerValue = triggerValue,
            ActionType = ActionType.SendReply,
            ActionValue = "Thanks",
            IsActive = true
        };
}
