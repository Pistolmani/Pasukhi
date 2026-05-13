using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.BotReadiness;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/bot-readiness")]
[Authorize]
public class BotReadinessController : ControllerBase
{
    private readonly IBotReadinessService _botReadiness;

    public BotReadinessController(IBotReadinessService botReadiness) =>
        _botReadiness = botReadiness;

    [HttpGet("template")]
    public async Task<IActionResult> GetTemplate(CancellationToken ct) =>
        Ok(await _botReadiness.GetTemplateAsync(ct));

    [HttpGet("report")]
    public async Task<IActionResult> GetReport(CancellationToken ct) =>
        Ok(await _botReadiness.GetReportAsync(ct));

    [HttpPut("answers")]
    public async Task<IActionResult> SaveAnswers(
        [FromBody] SaveBotAnswersRequest request, CancellationToken ct) =>
        Ok(await _botReadiness.SaveAnswersAsync(request, ct));

    [HttpPost("suggestions/generate")]
    public async Task<IActionResult> GenerateSuggestions(CancellationToken ct) =>
        Ok(await _botReadiness.GenerateSuggestionsAsync(ct));

    [HttpPatch("suggestions/{id:guid}/approve")]
    public async Task<IActionResult> ApproveSuggestion(Guid id, CancellationToken ct) =>
        Ok(await _botReadiness.ApproveSuggestionAsync(id, ct));

    [HttpPatch("suggestions/{id:guid}/reject")]
    public async Task<IActionResult> RejectSuggestion(Guid id, CancellationToken ct) =>
        Ok(await _botReadiness.RejectSuggestionAsync(id, ct));
}
