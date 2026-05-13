using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Conversations;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class ConversationService : IConversationService
{
    private const int LastMessageSnippetLength = 100;
    private const int MessagePageSize = 50;

    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ChannelWriter<OutboundMessageReadyEvent> _outboundWriter;

    public ConversationService(PasukhiDbContext db, ITenantProvider tenantProvider, ChannelWriter<OutboundMessageReadyEvent> outboundWriter)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _outboundWriter = outboundWriter;
    }

    public async Task<List<ConversationListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _db.Conversations
            .AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.ChannelType,
                c.ExternalCustomerId,
                c.CustomerDisplayName,
                c.Status,
                c.IsEscalated,
                c.UnreadCount,
                c.LastMessageAt,
                LastMessageSnippet = c.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.TextContent ?? m.MediaUrl)
                    .FirstOrDefault(),
                c.CreatedAt,
                c.UpdatedAt
            })
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ToListAsync(ct);

        return rows
            .Select(c => new ConversationListItemDto(
                c.Id,
                c.ChannelType,
                c.ExternalCustomerId,
                c.CustomerDisplayName,
                c.Status,
                c.IsEscalated,
                c.UnreadCount,
                c.LastMessageAt,
                Truncate(c.LastMessageSnippet),
                c.CreatedAt,
                c.UpdatedAt))
            .ToList();
    }

    public async Task<ConversationDetailDto?> GetByIdAsync(Guid id, DateTime? before = null, CancellationToken ct = default)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversation is null)
            return null;

        var messagesQuery = _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id);

        if (before is not null)
        {
            messagesQuery = messagesQuery.Where(m => m.CreatedAt < before.Value);
        }

        var messages = await messagesQuery
            .OrderByDescending(m => m.CreatedAt)
            .Take(MessagePageSize + 1)
            .Select(m => new MessageDto(
                m.Id,
                m.Direction,
                m.Source,
                m.MessageType,
                m.TextContent,
                m.MediaUrl,
                m.DeliveryStatus,
                m.CreatedAt))
            .ToListAsync(ct);

        var hasMoreMessages = messages.Count > MessagePageSize;
        if (hasMoreMessages)
        {
            messages.RemoveAt(messages.Count - 1);
        }

        messages.Reverse();
        var nextMessagesCursor = hasMoreMessages ? messages.FirstOrDefault()?.CreatedAt : null;

        if (conversation.UnreadCount != 0)
        {
            conversation.UnreadCount = 0;
            await _db.SaveChangesAsync(ct);
        }

        return new ConversationDetailDto(
            conversation.Id,
            conversation.ChannelType,
            conversation.ExternalCustomerId,
            conversation.CustomerDisplayName,
            conversation.Status,
            conversation.IsEscalated,
            conversation.UnreadCount,
            conversation.LastMessageAt,
            hasMoreMessages,
            nextMessagesCursor,
            messages,
            conversation.CreatedAt,
            conversation.UpdatedAt);
    }

    public async Task SendReplyAsync(Guid conversationId, SendReplyRequest request, CancellationToken ct = default)
    {
        var businessId = EnsureTenant();
        var text = request.TextContent.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Reply text is required.");
        }

        var conversation = await _db.Conversations
            .Include(c => c.ChannelConnection)
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        var messageId = Guid.NewGuid();
        var message = new Message
        {
            Id = messageId,
            BusinessId = businessId,
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Source = MessageSource.OperatorManual,
            MessageType = MessageType.Text,
            TextContent = text,
            ExternalSenderId = conversation.ChannelConnection.ExternalAccountId,
            ExternalMessageId = $"pending:{messageId}",
            DeliveryStatus = DeliveryStatus.Pending
        };

        _db.Messages.Add(message);
        conversation.LastMessageAt = DateTime.UtcNow;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var metric = await _db.DailyMetrics
            .FirstOrDefaultAsync(m =>
                m.BusinessId == businessId &&
                m.Date == today &&
                m.ChannelType == conversation.ChannelType, ct);

        if (metric is null)
        {
            metric = new DailyMetric
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Date = today,
                ChannelType = conversation.ChannelType,
                TotalOutbound = 1
            };
            _db.DailyMetrics.Add(metric);
        }
        else
        {
            metric.TotalOutbound += 1;
        }

        await _db.SaveChangesAsync(ct);

        await _outboundWriter.WriteAsync(new OutboundMessageReadyEvent
        {
            BusinessId = businessId,
            MessageId = message.Id,
            ConversationId = conversation.Id,
            ChannelConnectionId = conversation.ChannelConnectionId,
            ChannelType = conversation.ChannelType.ToString(),
            ExternalCustomerId = conversation.ExternalCustomerId,
            TextContent = text
        }, ct);
    }

    private Guid EnsureTenant()
    {
        if (_tenantProvider.BusinessId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        return _tenantProvider.BusinessId;
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return value.Length <= LastMessageSnippetLength
            ? value
            : value[..LastMessageSnippetLength];
    }
}
