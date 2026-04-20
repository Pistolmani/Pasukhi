using Pasukhi.Application.DTOs.Conversations;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.DTOs.Escalations;

public record EscalationListItemDto(
    Guid Id,
    Guid ConversationId,
    EscalationReason Reason,
    string? Notes,
    string? AiRejectedResponse,
    bool IsResolved,
    DateTime? ResolvedAt,
    string ExternalCustomerId,
    string? CustomerDisplayName,
    ChannelType ChannelType,
    DateTime CreatedAt);

public record EscalationDetailDto(
    Guid Id,
    Guid ConversationId,
    EscalationReason Reason,
    string? Notes,
    string? AiRejectedResponse,
    bool IsResolved,
    DateTime? ResolvedAt,
    string? ResolvedByUserId,
    string ExternalCustomerId,
    string? CustomerDisplayName,
    ChannelType ChannelType,
    List<MessageDto> RecentMessages,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record ResolveEscalationRequest(string? Notes);
