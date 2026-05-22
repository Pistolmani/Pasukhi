using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Analytics;
using Pasukhi.Application.Interfaces;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly PasukhiDbContext _db;
    private readonly IPlanLimitsService _planLimits;

    public AnalyticsService(PasukhiDbContext db, IPlanLimitsService planLimits)
    {
        _db = db;
        _planLimits = planLimits;
    }

    public async Task<DashboardStatsDto> GetDashboardAsync(int days = 7, CancellationToken ct = default)
    {
        var planDefinition = await _planLimits.GetCurrentAsync(ct);
        var isFullAnalytics = planDefinition.FullAnalytics;

        // Basic tier: clamp to today only, skip heavy aggregations.
        var clampedDays = isFullAnalytics ? Math.Clamp(days, 1, 90) : 1;
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-clampedDays + 1));

        var metrics = await _db.DailyMetrics
            .AsNoTracking()
            .Where(m => m.Date >= cutoff)
            .ToListAsync(ct);

        var totalInbound = metrics.Sum(m => m.TotalInbound);
        var totalOutbound = metrics.Sum(m => m.TotalOutbound);
        var faqReplies = isFullAnalytics ? metrics.Sum(m => m.FaqReplies) : 0;
        var ruleReplies = isFullAnalytics ? metrics.Sum(m => m.RuleReplies) : 0;
        var aiReplies = metrics.Sum(m => m.AiReplies);
        var aiTokensUsed = isFullAnalytics ? metrics.Sum(m => m.AiTokensUsed) : 0;
        var escalations = metrics.Sum(m => m.Escalations);
        var autoReplies = faqReplies + ruleReplies + aiReplies;
        var autoReplyRate = totalInbound > 0 ? (double)autoReplies / totalInbound : 0;

        var channelBreakdown = isFullAnalytics
            ? metrics
                .Where(m => m.ChannelType.HasValue)
                .GroupBy(m => m.ChannelType!.Value)
                .OrderBy(g => g.Key)
                .Select(g => new ChannelBreakdownDto(
                    g.Key,
                    g.Sum(m => m.TotalInbound),
                    g.Sum(m => m.TotalOutbound),
                    g.Sum(m => m.FaqReplies),
                    g.Sum(m => m.RuleReplies),
                    g.Sum(m => m.AiReplies),
                    g.Sum(m => m.Escalations)))
                .ToList()
            : new List<ChannelBreakdownDto>();

        var dailyBreakdown = isFullAnalytics
            ? metrics
                .GroupBy(m => m.Date)
                .OrderBy(g => g.Key)
                .Select(g => new DailyBreakdownDto(
                    g.Key,
                    g.Sum(m => m.TotalInbound),
                    g.Sum(m => m.TotalOutbound),
                    g.Sum(m => m.FaqReplies),
                    g.Sum(m => m.RuleReplies),
                    g.Sum(m => m.AiReplies),
                    g.Sum(m => m.AiTokensUsed),
                    g.Sum(m => m.Escalations)))
                .ToList()
            : new List<DailyBreakdownDto>();

        // Derive current tier from the plan definition we already fetched.
        var tier = Pasukhi.Application.Plans.PlanLimits.ByTier
            .First(kv => kv.Value == planDefinition).Key;

        return new DashboardStatsDto(
            totalInbound,
            totalOutbound,
            faqReplies,
            ruleReplies,
            aiReplies,
            aiTokensUsed,
            escalations,
            autoReplyRate,
            channelBreakdown,
            dailyBreakdown,
            tier,
            isFullAnalytics);
    }
}
