using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Conversations;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversations;

    public ConversationsController(IConversationService conversations)
    {
        _conversations = conversations;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _conversations.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] DateTime? before,
        CancellationToken cancellationToken)
    {
        var result = await _conversations.GetByIdAsync(id, before, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> SendReply(
        Guid id,
        [FromBody] SendReplyRequest request,
        CancellationToken cancellationToken)
    {
        await _conversations.SendReplyAsync(id, request, cancellationToken);
        return Accepted();
    }
}
