using Pasukhi.Application.DTOs.BotReadiness;

namespace Pasukhi.Application.Interfaces;

public interface IBotReadinessService
{
    Task<BotReadinessTemplateDto> GetTemplateAsync(CancellationToken ct = default);
    Task<BotReadinessReportDto> GetReportAsync(CancellationToken ct = default);
    Task<BotReadinessReportDto> SaveAnswersAsync(SaveBotAnswersRequest request, CancellationToken ct = default);
    Task<BotReadinessReportDto> GenerateSuggestionsAsync(CancellationToken ct = default);
    Task<BotKnowledgeSuggestionDto> ApproveSuggestionAsync(Guid id, CancellationToken ct = default);
    Task<BotKnowledgeSuggestionDto> RejectSuggestionAsync(Guid id, CancellationToken ct = default);
}
