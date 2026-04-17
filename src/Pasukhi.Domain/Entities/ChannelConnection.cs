using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class ChannelConnection : TenantEntity
{
    public ChannelType ChannelType { get; set; }
    public string ExternalAccountId { get; set; } = string.Empty;
    public string? ExternalAccountName { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastWebhookAt { get; set; }
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}
