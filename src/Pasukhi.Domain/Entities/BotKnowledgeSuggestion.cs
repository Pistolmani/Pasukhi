using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class BotKnowledgeSuggestion : TenantEntity
{
    public SuggestionType Type { get; set; }
    public SuggestionStatus Status { get; set; } = SuggestionStatus.Pending;
    public List<string> SourceQuestionKeys { get; set; } = [];
    public string PayloadJson { get; set; } = "{}";
    public string DedupeHash { get; set; } = string.Empty;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
}
