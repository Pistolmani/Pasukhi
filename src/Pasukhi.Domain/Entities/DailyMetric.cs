using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class DailyMetric : TenantEntity
{
    public DateOnly Date { get; set; }
    public ChannelType? ChannelType { get; set; }
    public int TotalInbound { get; set; }
    public int TotalOutbound { get; set; }
    public int FaqReplies { get; set; }
    public int RuleReplies { get; set; }
    public int AiReplies { get; set; }
    public int AiTokensUsed { get; set; }
    public int Escalations { get; set; }
    public int? AvgResponseTimeMs { get; set; }
}
