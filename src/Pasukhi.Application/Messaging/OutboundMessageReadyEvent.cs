namespace Pasukhi.Application.Messaging;

public record OutboundMessageReadyEvent : ITenantScopedEvent
{
    public Guid BusinessId { get; init; }
    public Guid MessageId { get; init; }
    public Guid ConversationId { get; init; }
    public Guid ChannelConnectionId { get; init; }
    public string ChannelType { get; init; } = string.Empty;
    public string ExternalCustomerId { get; init; } = string.Empty;
    public string? TextContent { get; init; }
}
