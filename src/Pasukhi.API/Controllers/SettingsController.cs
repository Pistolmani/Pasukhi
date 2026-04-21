using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Settings;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;

    public SettingsController(ISettingsService settings)
    {
        _settings = settings;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await _settings.GetAsync(cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateBusinessSettingsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _settings.UpdateAsync(request, cancellationToken));
}
