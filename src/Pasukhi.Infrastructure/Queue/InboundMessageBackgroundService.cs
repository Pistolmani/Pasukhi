using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Infrastructure.Consumers;
using Pasukhi.Infrastructure.Tenant;

namespace Pasukhi.Infrastructure.Queue;

/// <summary>
/// Drains the inbound message channel and dispatches each event to
/// <see cref="InboundMessageConsumer.ProcessAsync"/> in a dedicated scope.
/// Replaces the MassTransit RabbitMQ consumer for in-process messaging.
/// </summary>
public sealed class InboundMessageBackgroundService : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];

    private readonly ChannelReader<InboundMessageEvent> _reader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InboundMessageBackgroundService> _logger;

    public InboundMessageBackgroundService(
        ChannelReader<InboundMessageEvent> reader,
        IServiceScopeFactory scopeFactory,
        ILogger<InboundMessageBackgroundService> logger)
    {
        _reader = reader;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _reader.ReadAllAsync(stoppingToken))
        {
            await ProcessWithRetryAsync(evt, stoppingToken);
        }
    }

    private async Task ProcessWithRetryAsync(InboundMessageEvent evt, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(evt.BusinessId);
                var consumer = scope.ServiceProvider.GetRequiredService<InboundMessageConsumer>();
                await consumer.ProcessAsync(evt, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (attempt < RetryDelays.Length)
            {
                _logger.LogWarning(
                    ex,
                    "InboundMessageConsumer attempt {Attempt} failed for ExternalMessageId={ExternalMessageId}. Retrying in {Delay}s.",
                    attempt + 1,
                    evt.ExternalMessageId,
                    RetryDelays[attempt].TotalSeconds);

                await Task.Delay(RetryDelays[attempt], ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "InboundMessageConsumer failed after {Attempts} attempts for ExternalMessageId={ExternalMessageId}. Dropping event.",
                    RetryDelays.Length + 1,
                    evt.ExternalMessageId);
            }
        }
    }
}
