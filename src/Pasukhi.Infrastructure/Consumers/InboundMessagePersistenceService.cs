using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Consumers;

public class InboundMessagePersistenceService : IInboundMessagePersistenceService
{
    private readonly PasukhiDbContext _db;
    private readonly IDailyMetricsService _metrics;
    private readonly ILogger<InboundMessagePersistenceService> _logger;

    public InboundMessagePersistenceService(
        PasukhiDbContext db,
        IDailyMetricsService metrics,
        ILogger<InboundMessagePersistenceService> logger)
    {
        _db = db;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<PersistenceResult?> PersistAsync(InboundMessageEvent e, CancellationToken ct = default)
    {
        if (e.BusinessId == Guid.Empty)
        {
            _logger.LogWarning("InboundMessageEvent dropped: empty BusinessId. ExternalMessageId={ExternalMessageId}", e.ExternalMessageId);
            return null;
        }

        // 1. Idempotency - skip if already persisted.
        var alreadyExists = await _db.Messages
            .AnyAsync(m => m.BusinessId == e.BusinessId && m.ExternalMessageId == e.ExternalMessageId, ct);
        if (alreadyExists)
        {
            _logger.LogDebug("Duplicate InboundMessage ignored. ExternalMessageId={ExternalMessageId}", e.ExternalMessageId);
            return null;
        }

        if (!Enum.TryParse<ChannelType>(e.ChannelType, ignoreCase: true, out var channelType))
        {
            _logger.LogWarning("InboundMessageEvent dropped: unknown ChannelType '{ChannelType}'", e.ChannelType);
            return null;
        }

        var messageType = Enum.TryParse<MessageType>(e.MessageType, ignoreCase: true, out var mt)
            ? mt
            : MessageType.Text;

        var timestamp = DateTime.UtcNow;

        // 2. Get-or-create Conversation.
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c =>
                c.ChannelConnectionId == e.ChannelConnectionId &&
                c.ExternalCustomerId == e.ExternalSenderId, ct);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                BusinessId = e.BusinessId,
                ChannelConnectionId = e.ChannelConnectionId,
                ChannelType = channelType,
                ExternalCustomerId = e.ExternalSenderId,
                CustomerDisplayName = e.SenderDisplayName,
                Status = ConversationStatus.Active,
                LastMessageAt = timestamp,
                UnreadCount = 0
            };
            _db.Conversations.Add(conversation);
        }

        // 3. Insert Message.
        var message = new Message
        {
            Id = Guid.NewGuid(),
            BusinessId = e.BusinessId,
            ConversationId = conversation.Id,
            Direction = MessageDirection.Inbound,
            Source = MessageSource.Customer,
            MessageType = messageType,
            TextContent = e.TextContent,
            MediaUrl = e.MediaUrl,
            MediaMimeType = e.MediaMimeType,
            ExternalSenderId = e.ExternalSenderId,
            SenderDisplayName = e.SenderDisplayName,
            ExternalMessageId = e.ExternalMessageId,
            ExternalTimestamp = e.ExternalTimestamp,
            DeliveryStatus = DeliveryStatus.Delivered,
            RawPayloadJson = e.RawPayloadJson
        };
        _db.Messages.Add(message);

        // 4. Bump conversation counters.
        conversation.LastMessageAt = timestamp;
        conversation.UnreadCount += 1;
        if (conversation.Status == ConversationStatus.Resolved || conversation.Status == ConversationStatus.Archived)
            conversation.Status = ConversationStatus.Active;

        // 5. Bump DailyMetric for today/channel.
        var metric = await _metrics.GetOrCreateAsync(e.BusinessId, channelType, ct);
        metric.TotalInbound += 1;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            // Lost an idempotency race — another worker already persisted this message.
            _logger.LogInformation(
                "InboundMessage concurrently persisted by another consumer. ExternalMessageId={ExternalMessageId}",
                e.ExternalMessageId);
            foreach (var entry in _db.ChangeTracker.Entries())
                entry.State = EntityState.Detached;
            return null;
        }

        _logger.LogInformation(
            "InboundMessage persisted. Business={BusinessId} Channel={ChannelType} Conversation={ConversationId} Message={MessageId}",
            e.BusinessId, channelType, conversation.Id, message.Id);

        return new PersistenceResult(conversation, message, channelType);
    }
}
