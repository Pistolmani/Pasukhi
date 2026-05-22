using NSubstitute;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Plans;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Services;

namespace Pasukhi.UnitTests.Services;

public class AnalyticsServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_aggregates_totals_channels_and_daily_rows()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        db.DailyMetrics.AddRange(
            new DailyMetric
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Date = today.AddDays(-1),
                ChannelType = ChannelType.Instagram,
                TotalInbound = 10,
                TotalOutbound = 7,
                FaqReplies = 3,
                RuleReplies = 2,
                AiReplies = 1,
                AiTokensUsed = 120,
                Escalations = 1
            },
            new DailyMetric
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Date = today,
                ChannelType = ChannelType.WhatsApp,
                TotalInbound = 5,
                TotalOutbound = 4,
                FaqReplies = 1,
                RuleReplies = 1,
                AiReplies = 2,
                AiTokensUsed = 80,
                Escalations = 0
            },
            new DailyMetric
            {
                Id = Guid.NewGuid(),
                BusinessId = Guid.NewGuid(),
                Date = today,
                ChannelType = ChannelType.Messenger,
                TotalInbound = 999,
                TotalOutbound = 999
            });
        await db.SaveChangesAsync();

        var service = new AnalyticsService(db, FullAnalyticsStub());
        var result = await service.GetDashboardAsync(days: 7);

        Assert.Equal(15, result.TotalInbound);
        Assert.Equal(11, result.TotalOutbound);
        Assert.Equal(4, result.FaqReplies);
        Assert.Equal(3, result.RuleReplies);
        Assert.Equal(3, result.AiReplies);
        Assert.Equal(200, result.AiTokensUsed);
        Assert.Equal(1, result.Escalations);
        Assert.Equal(10d / 15d, result.AutoReplyRate);
        Assert.True(result.IsFullAnalytics);

        Assert.Equal(2, result.ChannelBreakdown.Count);
        Assert.Contains(result.ChannelBreakdown, row => row.ChannelType == ChannelType.Instagram && row.TotalInbound == 10);
        Assert.Contains(result.ChannelBreakdown, row => row.ChannelType == ChannelType.WhatsApp && row.TotalInbound == 5);
        Assert.Equal(new[] { today.AddDays(-1), today }, result.DailyBreakdown.Select(row => row.Date).ToArray());
    }

    [Fact]
    public async Task GetDashboardAsync_returns_zero_rate_when_no_inbound()
    {
        await using var db = TestDb.Create();
        var service = new AnalyticsService(db, FullAnalyticsStub());

        var result = await service.GetDashboardAsync(days: 7);

        Assert.Equal(0, result.TotalInbound);
        Assert.Equal(0, result.AutoReplyRate);
        Assert.Empty(result.ChannelBreakdown);
        Assert.Empty(result.DailyBreakdown);
    }

    private static IPlanLimitsService FullAnalyticsStub()
    {
        var stub = Substitute.For<IPlanLimitsService>();
        stub.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(PlanLimits.ByTier[Pasukhi.Domain.Enums.SubscriptionTier.Pro]);
        return stub;
    }
}
