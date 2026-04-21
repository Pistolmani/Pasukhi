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
    List<DailyBreakdownDto> DailyBreakdown);
