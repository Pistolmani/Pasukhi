using Pasukhi.Application.DTOs.Conversations;

namespace Pasukhi.Application.Interfaces;

public interface IConversationService
{
    Task<List<ConversationListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<ConversationDetailDto?> GetByIdAsync(Guid id, DateTime? before = null, CancellationToken ct = default);
    Task SendReplyAsync(Guid conversationId, SendReplyRequest request, CancellationToken ct = default);
}
