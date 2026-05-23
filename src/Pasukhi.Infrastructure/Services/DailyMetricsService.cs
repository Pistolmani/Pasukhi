using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class DailyMetricsService : IDailyMetricsService
{
    private readonly PasukhiDbContext _db;

    public DailyMetricsService(PasukhiDbContext db)
    {
        _db = db;
    }

    public async Task<DailyMetric> GetOrCreateAsync(Guid businessId, ChannelType channelType, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Check the change tracker FIRST. If a new DailyMetric for today was already
        // added in this DbContext scope (but not yet saved), a DB query won't find it —
        // which would create a second entity for the same (BusinessId, Date, ChannelType)
        // unique key and cause a constraint violation on SaveChangesAsync.
        var tracked = _db.ChangeTracker
            .Entries<DailyMetric>()
            .FirstOrDefault(e =>
                e.Entity.BusinessId == businessId &&
                e.Entity.Date == today &&
                e.Entity.ChannelType == channelType)
            ?.Entity;

        if (tracked is not null)
            return tracked;

        var metric = await _db.DailyMetrics.FirstOrDefaultAsync(m =>
            m.BusinessId == businessId &&
            m.Date == today &&
            m.ChannelType == channelType, ct);

        if (metric is not null)
            return metric;

        metric = new DailyMetric
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Date = today,
            ChannelType = channelType
        };
        _db.DailyMetrics.Add(metric);
        return metric;
    }
}
