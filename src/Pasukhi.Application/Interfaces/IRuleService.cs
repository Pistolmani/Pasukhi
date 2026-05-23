using Pasukhi.Application.DTOs.Rules;

namespace Pasukhi.Application.Interfaces;

public interface IRuleService : ICrudService<AutomationRuleDto, CreateAutomationRuleRequest, UpdateAutomationRuleRequest>
{
    Task UpdatePrioritiesAsync(UpdateRulePrioritiesRequest request, CancellationToken cancellationToken = default);
}
