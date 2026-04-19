using System.Text.Json.Serialization;

namespace Pasukhi.API.Webhooks;

public record MetaWebhookPayload
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = string.Empty;

    [JsonPropertyName("entry")]
    public List<MetaEntry> Entry { get; init; } = new();
}

public record MetaEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("messaging")]
    public List<MetaMessagingEvent>? Messaging { get; init; }

    [JsonPropertyName("changes")]
    public List<MetaChange>? Changes { get; init; }
}

public record MetaMessagingEvent
{
    [JsonPropertyName("sender")]
    public MetaParticipant Sender { get; init; } = new();

    [JsonPropertyName("recipient")]
    public MetaParticipant Recipient { get; init; } = new();

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    [JsonPropertyName("message")]
    public MetaMessage? Message { get; init; }
}

public record MetaParticipant
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}

public record MetaMessage
{
    [JsonPropertyName("mid")]
    public string Mid { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("is_echo")]
    public bool? IsEcho { get; init; }

    [JsonPropertyName("attachments")]
    public List<MetaAttachment>? Attachments { get; init; }
}

public record MetaAttachment
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("payload")]
    public MetaAttachmentPayload? Payload { get; init; }
}

public record MetaAttachmentPayload
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }
}

public record MetaChange
{
    [JsonPropertyName("value")]
    public MetaChangeValue? Value { get; init; }

    [JsonPropertyName("field")]
    public string Field { get; init; } = string.Empty;
}

public record MetaChangeValue
{
    [JsonPropertyName("messaging_product")]
    public string? MessagingProduct { get; init; }

    [JsonPropertyName("metadata")]
    public MetaWhatsAppMetadata? Metadata { get; init; }

    [JsonPropertyName("contacts")]
    public List<MetaWhatsAppContact>? Contacts { get; init; }

    [JsonPropertyName("messages")]
    public List<MetaWhatsAppMessage>? Messages { get; init; }
}

public record MetaWhatsAppMetadata
{
    [JsonPropertyName("display_phone_number")]
    public string? DisplayPhoneNumber { get; init; }

    [JsonPropertyName("phone_number_id")]
    public string? PhoneNumberId { get; init; }
}

public record MetaWhatsAppContact
{
    [JsonPropertyName("profile")]
    public MetaWhatsAppProfile? Profile { get; init; }

    [JsonPropertyName("wa_id")]
    public string WaId { get; init; } = string.Empty;
}

public record MetaWhatsAppProfile
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public record MetaWhatsAppMessage
{
    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    public MetaWhatsAppText? Text { get; init; }

    [JsonPropertyName("image")]
    public MetaWhatsAppMedia? Image { get; init; }

    [JsonPropertyName("audio")]
    public MetaWhatsAppMedia? Audio { get; init; }

    [JsonPropertyName("video")]
    public MetaWhatsAppMedia? Video { get; init; }

    [JsonPropertyName("document")]
    public MetaWhatsAppMedia? Document { get; init; }
}

public record MetaWhatsAppText
{
    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;
}

public record MetaWhatsAppMedia
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}
