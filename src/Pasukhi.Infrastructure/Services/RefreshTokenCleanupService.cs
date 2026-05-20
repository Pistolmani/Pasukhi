using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RefreshTokenCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(30);

    public RefreshTokenCleanupService(IServiceProvider services, ILogger<RefreshTokenCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
                var cutoff = DateTime.UtcNow.Subtract(RetentionWindow);

                var deleted = await db.RefreshTokens
                    .Where(t => t.ExpiresAt < cutoff || (t.IsRevoked && t.CreatedAt < cutoff))
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                    _logger.LogInformation("Cleaned up {Count} expired refresh tokens", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Refresh token cleanup failed");
            }
        }
    }
}
