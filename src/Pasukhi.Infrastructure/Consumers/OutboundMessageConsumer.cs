using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Consumers;

public class OutboundMessageConsumer
{
    private readonly PasukhiDbContext _db;
    private readonly IChannelDispatcher _dispatcher;
    private readonly ILogger<OutboundMessageConsumer> _logger;

    public OutboundMessageConsumer(
        PasukhiDbContext db,
        IChannelDispatcher dispatcher,
        ILogger<OutboundMessageConsumer> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task ProcessAsync(OutboundMessageReadyEvent evt, CancellationToken ct)
    {
        var message = await _db.Messages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == evt.MessageId && m.BusinessId == evt.BusinessId, ct);

        if (message is null)
        {
            _logger.LogWarning("Outbound message {MessageId} was not found.", evt.MessageId);
            return;
        }

        var channel = await _db.ChannelConnections
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.Id == evt.ChannelConnectionId &&
                c.BusinessId == evt.BusinessId &&
                c.IsActive, ct);

        if (channel is null)
        {
            _logger.LogWarning("Outbound channel {ChannelConnectionId} was not found or inactive.", evt.ChannelConnectionId);
            message.DeliveryStatus = DeliveryStatus.Failed;
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (!Enum.TryParse<ChannelType>(evt.ChannelType, ignoreCase: true, out var channelType))
        {
            _logger.LogWarning("Outbound message {MessageId} has unknown channel type {ChannelType}.", evt.MessageId, evt.ChannelType);
            message.DeliveryStatus = DeliveryStatus.Failed;
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (channel.ChannelType != channelType)
        {
            _logger.LogWarning(
                "Outbound message {MessageId} channel mismatch. Event={EventChannelType} Connection={ConnectionChannelType}.",
                evt.MessageId,
                channelType,
                channel.ChannelType);
            message.DeliveryStatus = DeliveryStatus.Failed;
            await _db.SaveChangesAsync(ct);
            return;
        }

        try
        {
            var externalMessageId = await _dispatcher.SendAsync(channelType, evt, channel.AccessToken, channel.ExternalAccountId, ct);

            message.ExternalMessageId = externalMessageId;
            message.DeliveryStatus = DeliveryStatus.Sent;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Outbound message sent. Message={MessageId} ExternalMessageId={ExternalMessageId}",
                message.Id,
                externalMessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbound message {MessageId} failed to send.", message.Id);
            message.DeliveryStatus = DeliveryStatus.Failed;
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to persist Failed status for outbound message {MessageId}.", message.Id);
                throw;
            }
        }
    }
}
