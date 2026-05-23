using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Interfaces;

public interface IMessageAutomationOrchestrator
{
    Task RunAsync(
        InboundMessageEvent inboundEvent,
        Conversation conversation,
        Message message,
        ChannelType channelType,
        CancellationToken ct = default);
}
