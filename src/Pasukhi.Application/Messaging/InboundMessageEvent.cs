namespace Pasukhi.Application.Messaging;

public record InboundMessageEvent
{
    public Guid BusinessId { get; init; }
    public Guid ChannelConnectionId { get; init; }
    public string ChannelType { get; init; } = string.Empty;
    public string ExternalSenderId { get; init; } = string.Empty;
    public string? SenderDisplayName { get; init; }
    public string ExternalMessageId { get; init; } = string.Empty;
    public string? TextContent { get; init; }
    public string? MediaUrl { get; init; }
    public string? MediaMimeType { get; init; }
    public string MessageType { get; init; } = "Text";
    public string ExternalTimestamp { get; init; } = string.Empty;
    public string RawPayloadJson { get; init; } = string.Empty;
}
