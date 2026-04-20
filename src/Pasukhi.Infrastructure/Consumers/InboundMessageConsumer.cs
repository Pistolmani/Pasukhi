using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Consumers;

/// <summary>
/// Persists one inbound message per webhook event:
///   1. Idempotency check on (BusinessId, ExternalMessageId) backed by a unique index.
///   2. Get-or-create Conversation by (ChannelConnectionId, ExternalCustomerId).
///   3. Insert Message (Direction=Inbound, Source=Customer).
///   4. Bump Conversation.LastMessageAt / UnreadCount.
///   5. Get-or-create DailyMetric row for today/channel and increment TotalInbound.
/// All committed in a single SaveChanges. A DbUpdateException from the unique index
/// race is treated as a successful no-op (another worker already persisted it).
/// Tenant context is set by TenantContextFilter before Consume runs.
/// </summary>
public class InboundMessageConsumer : IConsumer<InboundMessageEvent>
{
    private readonly PasukhiDbContext _db;
    private readonly ILogger<InboundMessageConsumer> _logger;

    public InboundMessageConsumer(PasukhiDbContext db, ILogger<InboundMessageConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InboundMessageEvent> context)
    {
        var e = context.Message;
        var ct = context.CancellationToken;

        if (e.BusinessId == Guid.Empty)
        {
            _logger.LogWarning("InboundMessageEvent dropped: empty BusinessId. ExternalMessageId={ExternalMessageId}", e.ExternalMessageId);
            return;
        }

        // 1. Idempotency — skip if already persisted.
        var alreadyExists = await _db.Messages
            .AnyAsync(m => m.BusinessId == e.BusinessId && m.ExternalMessageId == e.ExternalMessageId, ct);
        if (alreadyExists)
        {
            _logger.LogDebug("Duplicate InboundMessage ignored. ExternalMessageId={ExternalMessageId}", e.ExternalMessageId);
            return;
        }

        var channelType = Enum.TryParse<ChannelType>(e.ChannelType, ignoreCase: true, out var ch)
            ? ch
            : (ChannelType?)null;
        if (channelType is null)
        {
            _logger.LogWarning("InboundMessageEvent dropped: unknown ChannelType '{ChannelType}'", e.ChannelType);
            return;
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
                ChannelType = channelType.Value,
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
        {
            conversation.Status = ConversationStatus.Active;
        }

        // 5. Bump DailyMetric for today/channel.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var metric = await _db.DailyMetrics
            .FirstOrDefaultAsync(m =>
                m.BusinessId == e.BusinessId &&
                m.Date == today &&
                m.ChannelType == channelType, ct);

        if (metric is null)
        {
            metric = new DailyMetric
            {
                Id = Guid.NewGuid(),
                BusinessId = e.BusinessId,
                Date = today,
                ChannelType = channelType,
                TotalInbound = 1
            };
            _db.DailyMetrics.Add(metric);
        }
        else
        {
            metric.TotalInbound += 1;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Lost an idempotency race with another consumer — another worker already
            // persisted this message. Drop our pending changes and treat as success.
            _logger.LogInformation(
                "InboundMessage concurrently persisted by another consumer. ExternalMessageId={ExternalMessageId}",
                e.ExternalMessageId);
            foreach (var entry in _db.ChangeTracker.Entries())
            {
                entry.State = EntityState.Detached;
            }
            return;
        }

        _logger.LogInformation(
            "InboundMessage persisted. Business={BusinessId} Channel={ChannelType} Conversation={ConversationId} Message={MessageId}",
            e.BusinessId, channelType, conversation.Id, message.Id);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // Npgsql PostgresException.SqlState "23505" = unique_violation.
        var inner = ex.InnerException;
        while (inner is not null)
        {
            var sqlState = inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
            if (sqlState == "23505") return true;
            inner = inner.InnerException;
        }
        return false;
    }
}
