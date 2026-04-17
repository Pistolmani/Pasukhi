using Pasukhi.Application.DTOs.Rules;

namespace Pasukhi.Application.Interfaces;

public interface IRuleService
{
    Task<List<AutomationRuleDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AutomationRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AutomationRuleDto> CreateAsync(CreateAutomationRuleRequest request, CancellationToken cancellationToken = default);
    Task<AutomationRuleDto> UpdateAsync(Guid id, UpdateAutomationRuleRequest request, CancellationToken cancellationToken = default);
    Task UpdatePrioritiesAsync(UpdateRulePrioritiesRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
