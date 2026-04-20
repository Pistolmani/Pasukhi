using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Conversations;
using Pasukhi.Application.DTOs.Escalations;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class EscalationService : IEscalationService
{
    private readonly PasukhiDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EscalationService(PasukhiDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<EscalationListItemDto>> GetAllAsync(
        bool includeResolved = false,
        CancellationToken ct = default)
    {
        var query = _db.Escalations
            .AsNoTracking()
            .Include(e => e.Conversation)
            .AsQueryable();

        if (!includeResolved)
            query = query.Where(e => !e.IsResolved);

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new EscalationListItemDto(
                e.Id,
                e.ConversationId,
                e.Reason,
                e.Notes,
                e.AiRejectedResponse,
                e.IsResolved,
                e.ResolvedAt,
                e.Conversation.ExternalCustomerId,
                e.Conversation.CustomerDisplayName,
                e.Conversation.ChannelType,
                e.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<EscalationDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var escalation = await _db.Escalations
            .Include(e => e.Conversation)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (escalation is null)
            return null;

        var messages = await _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == escalation.ConversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .Select(m => new MessageDto(
                m.Id,
                m.Direction,
                m.Source,
                m.MessageType,
                m.TextContent,
                m.MediaUrl,
                m.DeliveryStatus,
                m.CreatedAt))
            .ToListAsync(ct);

        messages.Reverse();

        return new EscalationDetailDto(
            escalation.Id,
            escalation.ConversationId,
            escalation.Reason,
            escalation.Notes,
            escalation.AiRejectedResponse,
            escalation.IsResolved,
            escalation.ResolvedAt,
            escalation.ResolvedByUserId,
            escalation.Conversation.ExternalCustomerId,
            escalation.Conversation.CustomerDisplayName,
            escalation.Conversation.ChannelType,
            messages,
            escalation.CreatedAt,
            escalation.UpdatedAt);
    }

    public async Task ResolveAsync(Guid id, ResolveEscalationRequest request, CancellationToken ct = default)
    {
        var escalation = await _db.Escalations
            .Include(e => e.Conversation)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException($"Escalation {id} not found.");

        escalation.IsResolved = true;
        escalation.ResolvedAt = DateTime.UtcNow;
        escalation.ResolvedByUserId = _httpContextAccessor.HttpContext?
            .User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(request.Notes))
            escalation.Notes = request.Notes.Trim();

        // If no other open escalations remain, un-escalate the conversation
        var hasOtherOpen = await _db.Escalations
            .AnyAsync(e => e.ConversationId == escalation.ConversationId
                           && e.Id != escalation.Id
                           && !e.IsResolved, ct);

        if (!hasOtherOpen)
        {
            escalation.Conversation.IsEscalated = false;
            if (escalation.Conversation.Status == ConversationStatus.Escalated)
                escalation.Conversation.Status = ConversationStatus.Active;
        }

        await _db.SaveChangesAsync(ct);
    }
}
