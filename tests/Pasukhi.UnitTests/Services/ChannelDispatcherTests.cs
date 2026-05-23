using NSubstitute;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Services;

namespace Pasukhi.UnitTests.Services;

public class ChannelDispatcherTests
{
    private static OutboundMessageReadyEvent NewEvent(string channelType = "Instagram") => new()
    {
        BusinessId = Guid.NewGuid(),
        MessageId = Guid.NewGuid(),
        ChannelConnectionId = Guid.NewGuid(),
        ChannelType = channelType,
        ExternalCustomerId = "cust-123",
        TextContent = "Hello!",
    };

    private static ChannelDispatcher NewDispatcher(
        IInstagramChannelProvider? instagram = null,
        IMessengerChannelProvider? messenger = null,
        IWhatsAppChannelProvider? whatsApp = null) =>
        new(
            instagram ?? Substitute.For<IInstagramChannelProvider>(),
            messenger ?? Substitute.For<IMessengerChannelProvider>(),
            whatsApp ?? Substitute.For<IWhatsAppChannelProvider>());

    [Fact]
    public async Task SendAsync_Instagram_calls_instagram_provider()
    {
        var instagram = Substitute.For<IInstagramChannelProvider>();
        instagram.SendMessageAsync("cust-123", "Hello!", "tok", Arg.Any<CancellationToken>())
            .Returns("ext-msg-1");
        var dispatcher = NewDispatcher(instagram: instagram);
        var evt = NewEvent("Instagram");

        var result = await dispatcher.SendAsync(ChannelType.Instagram, evt, "tok", "acct-1");

        Assert.Equal("ext-msg-1", result);
        await instagram.Received(1).SendMessageAsync("cust-123", "Hello!", "tok", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_Messenger_calls_messenger_provider()
    {
        var messenger = Substitute.For<IMessengerChannelProvider>();
        messenger.SendMessageAsync("cust-123", "Hello!", "tok", Arg.Any<CancellationToken>())
            .Returns("ext-msg-2");
        var dispatcher = NewDispatcher(messenger: messenger);
        var evt = NewEvent("Messenger");

        var result = await dispatcher.SendAsync(ChannelType.Messenger, evt, "tok", "acct-1");

        Assert.Equal("ext-msg-2", result);
        await messenger.Received(1).SendMessageAsync("cust-123", "Hello!", "tok", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhatsApp_calls_whatsapp_provider_with_account_id()
    {
        var whatsApp = Substitute.For<IWhatsAppChannelProvider>();
        whatsApp.SendMessageAsync("cust-123", "Hello!", "tok", "acct-wa", Arg.Any<CancellationToken>())
            .Returns("ext-msg-3");
        var dispatcher = NewDispatcher(whatsApp: whatsApp);
        var evt = NewEvent("WhatsApp");

        var result = await dispatcher.SendAsync(ChannelType.WhatsApp, evt, "tok", "acct-wa");

        Assert.Equal("ext-msg-3", result);
        await whatsApp.Received(1).SendMessageAsync("cust-123", "Hello!", "tok", "acct-wa", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_unknown_channel_type_throws()
    {
        var dispatcher = NewDispatcher();
        var evt = NewEvent();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync((ChannelType)99, evt, "tok", "acct-1"));
    }
}
