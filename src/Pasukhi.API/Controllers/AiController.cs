using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Ai;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IBusinessPromptService _prompts;

    public AiController(IBusinessPromptService prompts)
    {
        _prompts = prompts;
    }

    [HttpGet("prompt")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await _prompts.GetAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("prompt")]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertBusinessPromptRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _prompts.UpsertAsync(request, cancellationToken));
}
