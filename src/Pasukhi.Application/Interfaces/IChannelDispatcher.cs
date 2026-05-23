using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Interfaces;

public interface IChannelDispatcher
{
    Task<string> SendAsync(
        ChannelType channelType,
        OutboundMessageReadyEvent evt,
        string accessToken,
        string externalAccountId,
        CancellationToken ct = default);
}
