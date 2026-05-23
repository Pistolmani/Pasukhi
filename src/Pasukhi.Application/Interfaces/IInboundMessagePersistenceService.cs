using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Interfaces;

public record PersistenceResult(Conversation Conversation, Message Message, ChannelType ChannelType);

public interface IInboundMessagePersistenceService
{
    /// <summary>
    /// Persists the inbound message and returns the result, or null if the event
    /// should be dropped (duplicate, empty BusinessId, or unknown ChannelType).
    /// </summary>
    Task<PersistenceResult?> PersistAsync(InboundMessageEvent e, CancellationToken ct = default);
}
