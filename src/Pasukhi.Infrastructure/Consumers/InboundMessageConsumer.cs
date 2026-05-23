using Microsoft.Extensions.Logging;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Infrastructure.Consumers;

/// <summary>
/// Entry point for inbound webhook events. Delegates persistence to
/// IInboundMessagePersistenceService and automation to IMessageAutomationOrchestrator.
/// Tenant context is set by the background service before ProcessAsync runs.
/// </summary>
public class InboundMessageConsumer
{
    private readonly IInboundMessagePersistenceService _persistence;
    private readonly IMessageAutomationOrchestrator _orchestrator;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<InboundMessageConsumer> _logger;

    public InboundMessageConsumer(
        IInboundMessagePersistenceService persistence,
        IMessageAutomationOrchestrator orchestrator,
        ITenantProvider tenantProvider,
        ILogger<InboundMessageConsumer> logger)
    {
        _persistence = persistence;
        _orchestrator = orchestrator;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task ProcessAsync(InboundMessageEvent e, CancellationToken ct)
    {
        // Downstream code (e.g. settings reads, escalation paths) relies on the ambient
        // tenant context. The background service must populate it from the event before
        // calling us. Events with their own empty BusinessId are a data issue, handled
        // gracefully by the persistence service — don't throw on those.
        if (e.BusinessId != Guid.Empty && _tenantProvider.BusinessId != e.BusinessId)
            throw new InvalidOperationException(
                $"Tenant context mismatch for inbound event {e.ExternalMessageId}: " +
                $"event BusinessId={e.BusinessId}, ambient={_tenantProvider.BusinessId}. " +
                "The background service must populate ITenantContext before ProcessAsync.");

        var result = await _persistence.PersistAsync(e, ct);
        if (result is null)
            return;

        if (result.Message.MessageType != MessageType.Text || string.IsNullOrWhiteSpace(result.Message.TextContent))
        {
            _logger.LogDebug("Skipping automation for non-text or empty inbound message {MessageId}.", result.Message.Id);
            return;
        }

        await _orchestrator.RunAsync(e, result.Conversation, result.Message, result.ChannelType, ct);
    }
}
