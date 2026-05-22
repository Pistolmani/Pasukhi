using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.DTOs.Analytics;

public record ChannelBreakdownDto(
    ChannelType ChannelType,
    int TotalInbound,
    int TotalOutbound,
    int FaqReplies,
    int RuleReplies,
    int AiReplies,
    int Escalations);

public record DailyBreakdownDto(
    DateOnly Date,
    int TotalInbound,
    int TotalOutbound,
    int FaqReplies,
    int RuleReplies,
    int AiReplies,
    int AiTokensUsed,
    int Escalations);

public record DashboardStatsDto(
    int TotalInbound,
    int TotalOutbound,
    int FaqReplies,
    int RuleReplies,
    int AiReplies,
    int AiTokensUsed,
    int Escalations,
    double AutoReplyRate,
    List<ChannelBreakdownDto> ChannelBreakdown,
    List<DailyBreakdownDto> DailyBreakdown,
    /// <summary>Current subscription tier of the business.</summary>
    SubscriptionTier Tier,
    /// <summary>
    /// When false the response is Basic-only: today's totals, empty breakdowns.
    /// When true the full historical window and per-channel data are included.
    /// </summary>
    bool IsFullAnalytics);
