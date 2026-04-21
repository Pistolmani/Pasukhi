using Pasukhi.Application.DTOs.Analytics;

namespace Pasukhi.Application.Interfaces;

public interface IAnalyticsService
{
    Task<DashboardStatsDto> GetDashboardAsync(int days = 7, CancellationToken ct = default);
}
