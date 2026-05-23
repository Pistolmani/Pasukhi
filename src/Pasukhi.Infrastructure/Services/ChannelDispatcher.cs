using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Infrastructure.Services;

public class ChannelDispatcher : IChannelDispatcher
{
    private readonly IInstagramChannelProvider _instagram;
    private readonly IMessengerChannelProvider _messenger;
    private readonly IWhatsAppChannelProvider _whatsApp;

    public ChannelDispatcher(
        IInstagramChannelProvider instagram,
        IMessengerChannelProvider messenger,
        IWhatsAppChannelProvider whatsApp)
    {
        _instagram = instagram;
        _messenger = messenger;
        _whatsApp = whatsApp;
    }

    public Task<string> SendAsync(
        ChannelType channelType,
        OutboundMessageReadyEvent evt,
        string accessToken,
        string externalAccountId,
        CancellationToken ct = default) =>
        channelType switch
        {
            ChannelType.Instagram => _instagram.SendMessageAsync(evt.ExternalCustomerId, evt.TextContent, accessToken, ct),
            ChannelType.Messenger => _messenger.SendMessageAsync(evt.ExternalCustomerId, evt.TextContent, accessToken, ct),
            ChannelType.WhatsApp => _whatsApp.SendMessageAsync(evt.ExternalCustomerId, evt.TextContent, accessToken, externalAccountId, ct),
            _ => throw new InvalidOperationException($"Unsupported channel type {channelType}.")
        };
}
