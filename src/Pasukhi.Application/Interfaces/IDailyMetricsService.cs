using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Interfaces;

public interface IDailyMetricsService
{
    Task<DailyMetric> GetOrCreateAsync(Guid businessId, ChannelType channelType, CancellationToken ct = default);
}
