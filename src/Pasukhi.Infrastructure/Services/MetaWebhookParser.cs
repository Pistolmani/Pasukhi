using System.Globalization;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Application.Webhooks;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Infrastructure.Services;

public class MetaWebhookParser : IMetaWebhookParser
{
    public InboundMessageEvent? ParseInstagram(MetaWebhookPayload payload) =>
        ParseInstagramBatch(payload).FirstOrDefault();

    public InboundMessageEvent? ParseMessenger(MetaWebhookPayload payload) =>
        ParseMessengerBatch(payload).FirstOrDefault();

    public InboundMessageEvent? ParseWhatsApp(MetaWebhookPayload payload)
    {
        return ParseWhatsAppBatch(payload).FirstOrDefault();
    }

    public IReadOnlyList<InboundMessageEvent> ParseInstagramBatch(MetaWebhookPayload payload) =>
        ParsePageMessages(payload, ChannelType.Instagram);

    public IReadOnlyList<InboundMessageEvent> ParseMessengerBatch(MetaWebhookPayload payload) =>
        ParsePageMessages(payload, ChannelType.Messenger);

    public IReadOnlyList<InboundMessageEvent> ParseWhatsAppBatch(MetaWebhookPayload payload)
    {
        var events = new List<InboundMessageEvent>();

        foreach (var entry in payload.Entry)
        {
            if (entry.Changes is null) continue;

            foreach (var change in entry.Changes)
            {
                if (!string.Equals(change.Field, "messages", StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = change.Value;
                if (value?.Messages is null) continue;

                var phoneNumberId = value.Metadata?.PhoneNumberId;
                if (string.IsNullOrWhiteSpace(phoneNumberId)) continue;

                var contactMap = value.Contacts?
                    .Where(c => !string.IsNullOrWhiteSpace(c.WaId))
                    .ToDictionary(c => c.WaId, c => c.Profile?.Name)
                    ?? new Dictionary<string, string?>();

                foreach (var message in value.Messages)
                {
                    if (string.IsNullOrWhiteSpace(message.Id) || string.IsNullOrWhiteSpace(message.From))
                        continue;

                    contactMap.TryGetValue(message.From, out var displayName);
                    var (messageType, mediaUrl, mimeType) = ExtractWhatsAppMedia(message);

                    events.Add(new InboundMessageEvent
                    {
                        ChannelType = ChannelType.WhatsApp.ToString(),
                        ExternalAccountId = phoneNumberId,
                        ExternalSenderId = message.From,
                        SenderDisplayName = displayName,
                        ExternalMessageId = message.Id,
                        TextContent = message.Text?.Body,
                        MediaUrl = mediaUrl,
                        MediaMimeType = mimeType,
                        MessageType = messageType.ToString(),
                        ExternalTimestamp = message.Timestamp
                    });
                }
            }
        }

        return events;
    }

    private static IReadOnlyList<InboundMessageEvent> ParsePageMessages(MetaWebhookPayload payload, ChannelType channelType)
    {
        var events = new List<InboundMessageEvent>();

        foreach (var entry in payload.Entry)
        {
            if (entry.Messaging is null || string.IsNullOrWhiteSpace(entry.Id)) continue;

            foreach (var message in entry.Messaging)
            {
                if (message.Message is null) continue;
                if (message.Message.IsEcho == true) continue;
                if (string.IsNullOrWhiteSpace(message.Message.Mid)) continue;
                if (string.IsNullOrWhiteSpace(message.Sender.Id)) continue;

                var (messageType, mediaUrl, mimeType) = ExtractPageAttachment(message.Message.Attachments);

                events.Add(new InboundMessageEvent
                {
                    ChannelType = channelType.ToString(),
                    ExternalAccountId = entry.Id,
                    ExternalSenderId = message.Sender.Id,
                    ExternalMessageId = message.Message.Mid,
                    TextContent = message.Message.Text,
                    MediaUrl = mediaUrl,
                    MediaMimeType = mimeType,
                    MessageType = messageType.ToString(),
                    ExternalTimestamp = message.Timestamp.ToString(CultureInfo.InvariantCulture)
                });
            }
        }

        return events;
    }

    private static (MessageType type, string? url, string? mime) ExtractPageAttachment(List<MetaAttachment>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
            return (MessageType.Text, null, null);

        var attachment = attachments[0];
        var type = attachment.Type switch
        {
            "image" => MessageType.Image,
            "audio" => MessageType.Audio,
            "video" => MessageType.Video,
            "file" => MessageType.File,
            "sticker" => MessageType.Sticker,
            _ => MessageType.Text
        };

        return (type, attachment.Payload?.Url, attachment.Payload?.MimeType);
    }

    private static (MessageType type, string? url, string? mime) ExtractWhatsAppMedia(MetaWhatsAppMessage message)
    {
        return message.Type switch
        {
            "image" => (MessageType.Image, message.Image?.Url ?? message.Image?.Id, message.Image?.MimeType),
            "audio" => (MessageType.Audio, message.Audio?.Url ?? message.Audio?.Id, message.Audio?.MimeType),
            "video" => (MessageType.Video, message.Video?.Url ?? message.Video?.Id, message.Video?.MimeType),
            "document" => (MessageType.File, message.Document?.Url ?? message.Document?.Id, message.Document?.MimeType),
            _ => (MessageType.Text, null, null)
        };
    }
}
