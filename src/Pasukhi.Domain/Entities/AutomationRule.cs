using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class AutomationRule : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public TriggerType TriggerType { get; set; }
    public string TriggerValue { get; set; } = string.Empty;
    public ActionType ActionType { get; set; }
    public string ActionValue { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int MatchCount { get; set; }
}
