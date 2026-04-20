using Pasukhi.Application.Messaging;
using Pasukhi.Application.Webhooks;

namespace Pasukhi.Application.Interfaces;

public interface IMetaWebhookParser
{
    InboundMessageEvent? ParseInstagram(MetaWebhookPayload payload);
    InboundMessageEvent? ParseMessenger(MetaWebhookPayload payload);
    InboundMessageEvent? ParseWhatsApp(MetaWebhookPayload payload);
    IReadOnlyList<InboundMessageEvent> ParseInstagramBatch(MetaWebhookPayload payload);
    IReadOnlyList<InboundMessageEvent> ParseMessengerBatch(MetaWebhookPayload payload);
    IReadOnlyList<InboundMessageEvent> ParseWhatsAppBatch(MetaWebhookPayload payload);
}
