using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.DTOs.Conversations;

public record ConversationListItemDto(
    Guid Id,
    ChannelType ChannelType,
    string ExternalCustomerId,
    string? CustomerDisplayName,
    ConversationStatus Status,
    bool IsEscalated,
    int UnreadCount,
    DateTime? LastMessageAt,
    string? LastMessageSnippet,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record MessageDto(
    Guid Id,
    MessageDirection Direction,
    MessageSource Source,
    MessageType MessageType,
    string? TextContent,
    string? MediaUrl,
    DeliveryStatus DeliveryStatus,
    DateTime CreatedAt);

public record ConversationDetailDto(
    Guid Id,
    ChannelType ChannelType,
    string ExternalCustomerId,
    string? CustomerDisplayName,
    ConversationStatus Status,
    bool IsEscalated,
    int UnreadCount,
    DateTime? LastMessageAt,
    bool HasMoreMessages,
    DateTime? NextMessagesCursor,
    List<MessageDto> Messages,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SendReplyRequest(string TextContent);
