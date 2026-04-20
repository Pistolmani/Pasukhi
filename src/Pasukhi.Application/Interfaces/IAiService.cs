using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Interfaces;

public record AiFaqContextItem(
    Guid Id,
    string Question,
    string Answer,
    string? Keywords);

public record AiMessage(
    string Role,
    string Content,
    DateTime CreatedAt);

public record AiContext(
    Guid BusinessId,
    Guid ConversationId,
    Guid InboundMessageId,
    string BusinessName,
    string? BusinessDescription,
    string SystemPrompt,
    string ToneDescription,
    string EscalationMessage,
    bool IsAiEnabled,
    int MaxAiTokensPerDay,
    double AiConfidenceThreshold,
    ChannelType ChannelType,
    string CustomerDisplayName,
    string InboundMessageText,
    IReadOnlyList<AiFaqContextItem> RelevantFaqs,
    IReadOnlyList<AiMessage> ConversationHistory);

public record AiReplyResult(
    bool Success,
    string? ReplyText,
    double ConfidenceScore,
    bool ShouldEscalate,
    string? EscalationReason,
    int TokensUsed,
    TimeSpan ProcessingTime,
    string? Error = null);

public interface IAiService
{
    Task<AiReplyResult> GenerateReplyAsync(AiContext context, CancellationToken ct = default);
}
