using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Channels;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/channels")]
[Authorize]
public class ChannelsController : ControllerBase
{
    private readonly IChannelService _channels;
    private readonly IMessengerProfileService _messengerProfile;

    public ChannelsController(IChannelService channels, IMessengerProfileService messengerProfile)
    {
        _channels = channels;
        _messengerProfile = messengerProfile;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _channels.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _channels.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateChannelConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _channels.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateChannelConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _channels.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _channels.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("messenger-profile/sync")]
    public async Task<IActionResult> SyncMessengerProfile(
        [FromBody] SyncMessengerProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _messengerProfile.SyncAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("messenger-profile/greeting")]
    public async Task<IActionResult> GetMessengerGreeting(CancellationToken cancellationToken)
    {
        var text = await _messengerProfile.GetStoredGreetingTextAsync(cancellationToken);
        return Ok(new { greetingText = text });
    }
}
