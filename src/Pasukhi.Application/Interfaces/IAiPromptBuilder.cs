using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Interfaces;

public interface IAiPromptBuilder
{
    Task<AiContext?> BuildAsync(
        Conversation conversation,
        Message inboundMessage,
        ChannelType channelType,
        CancellationToken ct = default);
}
