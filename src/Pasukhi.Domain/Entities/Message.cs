using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class Message : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public MessageDirection Direction { get; set; }
    public MessageType MessageType { get; set; }
    public string? TextContent { get; set; }
    public string? MediaUrl { get; set; }
    public string? MediaMimeType { get; set; }
    public string ExternalSenderId { get; set; } = string.Empty;
    public string? SenderDisplayName { get; set; }
    public MessageSource Source { get; set; }
    public Guid? MatchedFaqItemId { get; set; }
    public Guid? MatchedRuleId { get; set; }
    public double? AiConfidenceScore { get; set; }
    public string ExternalMessageId { get; set; } = string.Empty;
    public string? ExternalTimestamp { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Pending;
    public string? RawPayloadJson { get; set; }
}
