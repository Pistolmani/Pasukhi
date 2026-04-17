using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class Conversation : TenantEntity
{
    public Guid ChannelConnectionId { get; set; }
    public ChannelConnection ChannelConnection { get; set; } = null!;
    public ChannelType ChannelType { get; set; }
    public string ExternalCustomerId { get; set; } = string.Empty;
    public string? CustomerDisplayName { get; set; }
    public string? CustomerProfilePictureUrl { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Active;
    public bool IsEscalated { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<Escalation> Escalations { get; set; } = new List<Escalation>();
}
