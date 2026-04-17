using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.DTOs.Rules;

public record AutomationRuleDto(
    Guid Id,
    Guid BusinessId,
    string Name,
    int Priority,
    TriggerType TriggerType,
    string TriggerValue,
    ActionType ActionType,
    string ActionValue,
    bool IsActive,
    int MatchCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateAutomationRuleRequest(
    string Name,
    int Priority,
    TriggerType TriggerType,
    string TriggerValue,
    ActionType ActionType,
    string ActionValue,
    bool IsActive);

public record UpdateAutomationRuleRequest(
    string Name,
    int Priority,
    TriggerType TriggerType,
    string TriggerValue,
    ActionType ActionType,
    string ActionValue,
    bool IsActive);

public record RulePriorityItem(Guid Id, int Priority);

public record UpdateRulePrioritiesRequest(IReadOnlyList<RulePriorityItem> Items);
