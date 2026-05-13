# Codex Task — Phase 6: Conversations & Escalations API

> Read `AGENTS.md` first. Phases 0–5 must be complete before starting this.

## Goal

By the end of this phase:
- Operators can list conversations, open a conversation detail with paginated messages, and send a manual reply
- Opening a conversation clears its `UnreadCount`
- The `Escalation` entity exists and is persisted by the consumer (Phase 7 wires it)
- Operators can list, view, and resolve escalations
- The frontend has Conversations and Escalations pages

---

## Repo root

`C:\Users\piros\OneDrive\Desktop\Pasukhi\`

---

## Step 1 — Escalation Entity

### `src/Pasukhi.Domain/Entities/Escalation.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class Escalation : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public EscalationReason Reason { get; set; }
    public string? Notes { get; set; }
    public string? AiRejectedResponse { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByUserId { get; set; }
}
```

### `src/Pasukhi.Domain/Enums/EscalationReason.cs`

```csharp
namespace Pasukhi.Domain.Enums;

public enum EscalationReason
{
    NoMatch,
    LowAiConfidence,
    SafetyCheckFailed,
    CustomerRequested,
    OperatorTriggered
}
```

Add `Escalations` DbSet and global query filter to `PasukhiDbContext`:

```csharp
public DbSet<Escalation> Escalations => Set<Escalation>();
```

In `OnModelCreating`:

```csharp
modelBuilder.Entity<Escalation>()
    .HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
```

---

## Step 2 — DTOs

### `src/Pasukhi.Application/DTOs/Conversations/ConversationDtos.cs`

```csharp
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
```

### `src/Pasukhi.Application/DTOs/Escalations/EscalationDtos.cs`

```csharp
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
```

---

## Step 3 — Service Interfaces

### `src/Pasukhi.Application/Interfaces/IConversationService.cs`

```csharp
using Pasukhi.Application.DTOs.Conversations;

namespace Pasukhi.Application.Interfaces;

public interface IConversationService
{
    Task<List<ConversationListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<ConversationDetailDto?> GetByIdAsync(Guid id, DateTime? before = null, CancellationToken ct = default);
    Task SendReplyAsync(Guid conversationId, SendReplyRequest request, CancellationToken ct = default);
}
```

### `src/Pasukhi.Application/Interfaces/IEscalationService.cs`

```csharp
using Pasukhi.Application.DTOs.Escalations;

namespace Pasukhi.Application.Interfaces;

public interface IEscalationService
{
    Task<List<EscalationListItemDto>> GetAllAsync(bool includeResolved = false, CancellationToken ct = default);
    Task<EscalationDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task ResolveAsync(Guid id, ResolveEscalationRequest request, CancellationToken ct = default);
}
```

---

## Step 4 — Service Implementations

### `src/Pasukhi.Infrastructure/Services/ConversationService.cs`

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Conversations;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class ConversationService : IConversationService
{
    private const int LastMessageSnippetLength = 100;
    private const int MessagePageSize = 50;

    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPublishEndpoint _bus;

    public ConversationService(PasukhiDbContext db, ITenantProvider tenantProvider, IPublishEndpoint bus)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _bus = bus;
    }

    public async Task<List<ConversationListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _db.Conversations
            .AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.ChannelType,
                c.ExternalCustomerId,
                c.CustomerDisplayName,
                c.Status,
                c.IsEscalated,
                c.UnreadCount,
                c.LastMessageAt,
                LastMessageSnippet = c.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.TextContent ?? m.MediaUrl)
                    .FirstOrDefault(),
                c.CreatedAt,
                c.UpdatedAt
            })
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ToListAsync(ct);

        return rows
            .Select(c => new ConversationListItemDto(
                c.Id,
                c.ChannelType,
                c.ExternalCustomerId,
                c.CustomerDisplayName,
                c.Status,
                c.IsEscalated,
                c.UnreadCount,
                c.LastMessageAt,
                Truncate(c.LastMessageSnippet),
                c.CreatedAt,
                c.UpdatedAt))
            .ToList();
    }

    public async Task<ConversationDetailDto?> GetByIdAsync(Guid id, DateTime? before = null, CancellationToken ct = default)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversation is null)
            return null;

        var messagesQuery = _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id);

        if (before is not null)
            messagesQuery = messagesQuery.Where(m => m.CreatedAt < before.Value);

        var messages = await messagesQuery
            .OrderByDescending(m => m.CreatedAt)
            .Take(MessagePageSize + 1)
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

        var hasMoreMessages = messages.Count > MessagePageSize;
        if (hasMoreMessages)
            messages.RemoveAt(messages.Count - 1);

        messages.Reverse();
        var nextMessagesCursor = hasMoreMessages ? messages.FirstOrDefault()?.CreatedAt : null;

        if (conversation.UnreadCount != 0)
        {
            conversation.UnreadCount = 0;
            await _db.SaveChangesAsync(ct);
        }

        return new ConversationDetailDto(
            conversation.Id,
            conversation.ChannelType,
            conversation.ExternalCustomerId,
            conversation.CustomerDisplayName,
            conversation.Status,
            conversation.IsEscalated,
            conversation.UnreadCount,
            conversation.LastMessageAt,
            hasMoreMessages,
            nextMessagesCursor,
            messages,
            conversation.CreatedAt,
            conversation.UpdatedAt);
    }

    public async Task SendReplyAsync(Guid conversationId, SendReplyRequest request, CancellationToken ct = default)
    {
        var businessId = EnsureTenant();
        var text = request.TextContent.Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Reply text is required.");

        var conversation = await _db.Conversations
            .Include(c => c.ChannelConnection)
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        var messageId = Guid.NewGuid();
        var message = new Message
        {
            Id = messageId,
            BusinessId = businessId,
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Source = MessageSource.OperatorManual,
            MessageType = MessageType.Text,
            TextContent = text,
            ExternalSenderId = conversation.ChannelConnection.ExternalAccountId,
            ExternalMessageId = $"pending:{messageId}",
            DeliveryStatus = DeliveryStatus.Pending
        };

        _db.Messages.Add(message);
        conversation.LastMessageAt = DateTime.UtcNow;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var metric = await _db.DailyMetrics
            .FirstOrDefaultAsync(m =>
                m.BusinessId == businessId &&
                m.Date == today &&
                m.ChannelType == conversation.ChannelType, ct);

        if (metric is null)
        {
            metric = new DailyMetric
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Date = today,
                ChannelType = conversation.ChannelType,
                TotalOutbound = 1
            };
            _db.DailyMetrics.Add(metric);
        }
        else
        {
            metric.TotalOutbound += 1;
        }

        await _db.SaveChangesAsync(ct);

        await _bus.Publish(new OutboundMessageReadyEvent
        {
            BusinessId = businessId,
            MessageId = message.Id,
            ConversationId = conversation.Id,
            ChannelConnectionId = conversation.ChannelConnectionId,
            ChannelType = conversation.ChannelType.ToString(),
            ExternalCustomerId = conversation.ExternalCustomerId,
            TextContent = text
        }, ct);
    }

    private Guid EnsureTenant()
    {
        if (_tenantProvider.BusinessId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required.");
        return _tenantProvider.BusinessId;
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        return value.Length <= LastMessageSnippetLength ? value : value[..LastMessageSnippetLength];
    }
}
```

### `src/Pasukhi.Infrastructure/Services/EscalationService.cs`

```csharp
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

    public async Task<List<EscalationListItemDto>> GetAllAsync(bool includeResolved = false, CancellationToken ct = default)
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

        if (escalation is null) return null;

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
```

---

## Step 5 — Controllers

### `src/Pasukhi.API/Controllers/ConversationsController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Conversations;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversations;

    public ConversationsController(IConversationService conversations)
    {
        _conversations = conversations;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _conversations.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] DateTime? before,
        CancellationToken cancellationToken)
    {
        var result = await _conversations.GetByIdAsync(id, before, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> SendReply(
        Guid id,
        [FromBody] SendReplyRequest request,
        CancellationToken cancellationToken)
    {
        await _conversations.SendReplyAsync(id, request, cancellationToken);
        return Accepted();
    }
}
```

### `src/Pasukhi.API/Controllers/EscalationsController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Escalations;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/escalations")]
[Authorize]
public class EscalationsController : ControllerBase
{
    private readonly IEscalationService _escalations;

    public EscalationsController(IEscalationService escalations)
    {
        _escalations = escalations;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeResolved = false,
        CancellationToken cancellationToken = default) =>
        Ok(await _escalations.GetAllAsync(includeResolved, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _escalations.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid id,
        [FromBody] ResolveEscalationRequest request,
        CancellationToken cancellationToken)
    {
        await _escalations.ResolveAsync(id, request, cancellationToken);
        return NoContent();
    }
}
```

---

## Step 6 — Register Services in Program.cs

```csharp
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IEscalationService, EscalationService>();
builder.Services.AddHttpContextAccessor();
```

---

## Step 7 — Migration

```bash
dotnet ef migrations add AddEscalations --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
dotnet ef database update --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
```

---

## Step 8 — Frontend: Conversations Page

### `pasukhi-admin/src/pages/Conversations.tsx`

```tsx
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { api } from '@/lib/api';
import { Badge } from '@/components/ui/badge';

interface ConversationListItem {
  id: string;
  channelType: string;
  customerDisplayName: string | null;
  externalCustomerId: string;
  status: string;
  isEscalated: boolean;
  unreadCount: number;
  lastMessageAt: string | null;
  lastMessageSnippet: string | null;
}

export default function Conversations() {
  const { data: conversations = [], isLoading } = useQuery<ConversationListItem[]>({
    queryKey: ['conversations'],
    queryFn: () => api.get('/api/conversations').then(r => r.data),
    refetchInterval: 10_000,
  });

  if (isLoading) return <div className="p-6">Loading…</div>;

  return (
    <div className="p-6 space-y-4">
      <h1 className="text-2xl font-semibold">Conversations</h1>
      <div className="divide-y rounded-md border">
        {conversations.map(c => (
          <Link
            key={c.id}
            to={`/conversations/${c.id}`}
            className="flex items-center justify-between px-4 py-3 hover:bg-muted/50 transition-colors"
          >
            <div className="min-w-0">
              <p className="font-medium truncate">
                {c.customerDisplayName ?? c.externalCustomerId}
              </p>
              {c.lastMessageSnippet && (
                <p className="text-sm text-muted-foreground truncate">{c.lastMessageSnippet}</p>
              )}
            </div>
            <div className="flex items-center gap-2 shrink-0 ml-4">
              <Badge variant="outline">{c.channelType}</Badge>
              {c.isEscalated && <Badge variant="destructive">Escalated</Badge>}
              {c.unreadCount > 0 && (
                <span className="rounded-full bg-primary text-primary-foreground text-xs px-2 py-0.5">
                  {c.unreadCount}
                </span>
              )}
            </div>
          </Link>
        ))}
        {conversations.length === 0 && (
          <p className="p-4 text-sm text-muted-foreground">No conversations yet.</p>
        )}
      </div>
    </div>
  );
}
```

Add routes and nav links in your router/sidebar:

```tsx
<Route path="/conversations" element={<Conversations />} />
<Route path="/conversations/:id" element={<ConversationDetail />} />
<Route path="/escalations" element={<Escalations />} />
```

---

## Verification

```bash
dotnet build
cd pasukhi-admin && npx tsc --noEmit
```

Test:
```bash
# List conversations (after receiving at least one webhook)
curl -H "Authorization: Bearer <token>" http://localhost:5000/api/conversations

# List open escalations
curl -H "Authorization: Bearer <token>" http://localhost:5000/api/escalations
```

---

## Commit

```bash
git add src/ pasukhi-admin/src/ docs/codex/phase-6.md
git commit -m "feat(06): conversations and escalations API + frontend pages"
```

---

## What's Next

Phase 7: `docs/codex/phase-7.md` — Wire the complete auto-reply pipeline: FAQ → Rule → Escalate inside `InboundMessageConsumer`.
