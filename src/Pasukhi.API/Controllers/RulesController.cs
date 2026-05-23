using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Rules;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/rules")]
[Authorize]
public class RulesController : CrudControllerBase<AutomationRuleDto, CreateAutomationRuleRequest, UpdateAutomationRuleRequest, IRuleService>
{
    public RulesController(IRuleService rules) : base(rules) { }

    protected override Guid GetEntityId(AutomationRuleDto dto) => dto.Id;

    [HttpPut("priorities")]
    public async Task<IActionResult> UpdatePriorities(
        [FromBody] UpdateRulePrioritiesRequest request,
        CancellationToken cancellationToken)
    {
        await Service.UpdatePrioritiesAsync(request, cancellationToken);
        return NoContent();
    }
}
