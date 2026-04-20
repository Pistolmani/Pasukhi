using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Escalations;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/escalations")]
[Authorize]
public class EscalationsController : ControllerBase
{
    private readonly IEscalationService _escalations;

    public EscalationsController(IEscalationService escalations)
    {
        _escalations = escalations;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeResolved = false,
        CancellationToken cancellationToken = default) =>
        Ok(await _escalations.GetAllAsync(includeResolved, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _escalations.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid id,
        [FromBody] ResolveEscalationRequest request,
        CancellationToken cancellationToken)
    {
        await _escalations.ResolveAsync(id, request, cancellationToken);
        return NoContent();
    }
}
