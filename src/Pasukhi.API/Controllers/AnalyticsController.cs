using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public AnalyticsController(IAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default) =>
        Ok(await _analytics.GetDashboardAsync(Math.Clamp(days, 1, 90), cancellationToken));
}
