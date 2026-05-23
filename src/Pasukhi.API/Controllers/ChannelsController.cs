using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Channels;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/channels")]
[Authorize]
public class ChannelsController : CrudControllerBase<ChannelConnectionDto, CreateChannelConnectionRequest, UpdateChannelConnectionRequest, IChannelService>
{
    private readonly IMessengerProfileService _messengerProfile;

    public ChannelsController(IChannelService channels, IMessengerProfileService messengerProfile) : base(channels)
    {
        _messengerProfile = messengerProfile;
    }

    protected override Guid GetEntityId(ChannelConnectionDto dto) => dto.Id;

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
