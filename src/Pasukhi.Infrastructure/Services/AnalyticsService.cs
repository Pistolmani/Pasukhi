using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Analytics;
using Pasukhi.Application.Interfaces;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly PasukhiDbContext _db;

    public AnalyticsService(PasukhiDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardStatsDto> GetDashboardAsync(int days = 7, CancellationToken ct = default)
    {
        var clampedDays = Math.Clamp(days, 1, 90);
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-clampedDays + 1));

        var metrics = await _db.DailyMetrics
            .AsNoTracking()
            .Where(m => m.Date >= cutoff)
            .ToListAsync(ct);

        var totalInbound = metrics.Sum(m => m.TotalInbound);
        var totalOutbound = metrics.Sum(m => m.TotalOutbound);
        var faqReplies = metrics.Sum(m => m.FaqReplies);
        var ruleReplies = metrics.Sum(m => m.RuleReplies);
        var aiReplies = metrics.Sum(m => m.AiReplies);
        var aiTokensUsed = metrics.Sum(m => m.AiTokensUsed);
        var escalations = metrics.Sum(m => m.Escalations);
        var autoReplies = faqReplies + ruleReplies + aiReplies;
        var autoReplyRate = totalInbound > 0 ? (double)autoReplies / totalInbound : 0;

        var channelBreakdown = metrics
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
            .ToList();

        var dailyBreakdown = metrics
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
            .ToList();

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
            dailyBreakdown);
    }
}
