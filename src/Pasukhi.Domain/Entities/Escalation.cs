using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class Escalation : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public EscalationReason Reason { get; set; }
    public string? Notes { get; set; }
    public string? AiRejectedResponse { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByUserId { get; set; }
}
