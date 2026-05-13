# Codex Task — Phase 4: Tenant Context + Message & Conversation Persistence

> Read `AGENTS.md` first. Phases 0, 1, 2, and 3 must be complete before starting this.

## Goal

By the end of this phase:
- The scoped `ITenantContext` is seeded from JWT claims on every HTTP request
- The same `ITenantContext` is seeded from the queue event's `BusinessId` inside MassTransit consumers
- `Conversation`, `Message`, and `DailyMetric` domain entities exist
- `InboundMessageConsumer` persists one inbound message per webhook event (idempotency, get-or-create conversation, DailyMetric counter) and returns without running automation
- A unique index on `(BusinessId, ExternalMessageId)` enforces idempotency at the DB level

---

## Repo root

`C:\Users\piros\OneDrive\Desktop\Pasukhi\`

---

## Step 1 — Tenant Interfaces

### `src/Pasukhi.Application/Interfaces/ITenantProvider.cs`

```csharp
namespace Pasukhi.Application.Interfaces;

public interface ITenantProvider
{
    Guid BusinessId { get; }
}
```

### `src/Pasukhi.Application/Interfaces/ITenantContext.cs`

```csharp
namespace Pasukhi.Application.Interfaces;

/// <summary>
/// Mutable tenant context used by both HTTP requests (seeded from JWT) and
/// MassTransit consumers (seeded from the incoming event). Extends ITenantProvider
/// so existing read-only consumers (DbContext global filters, services) keep working.
/// </summary>
public interface ITenantContext : ITenantProvider
{
    void SetBusinessId(Guid businessId);
}
```

### `src/Pasukhi.Infrastructure/Tenant/TenantContext.cs`

```csharp
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Tenant;

/// <summary>
/// Scoped holder for the current tenant's BusinessId. Mutable so middleware (HTTP)
/// and MassTransit consume filters (queue) can seed it before downstream code runs.
/// </summary>
public class TenantContext : ITenantContext
{
    public Guid BusinessId { get; private set; }

    public void SetBusinessId(Guid businessId) => BusinessId = businessId;
}
```

---

## Step 2 — Tenant Context Middleware (HTTP)

### `src/Pasukhi.API/Middleware/TenantContextMiddleware.cs`

```csharp
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Middleware;

/// <summary>
/// Seeds the scoped <see cref="ITenantContext"/> from the authenticated user's
/// <c>BusinessId</c> claim. Runs after auth; if the user is anonymous or has no
/// claim, BusinessId stays at Guid.Empty and DbContext global filters return no rows.
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var claim = context.User.FindFirst("BusinessId")?.Value;
        if (Guid.TryParse(claim, out var businessId))
        {
            tenantContext.SetBusinessId(businessId);
        }

        await _next(context);
    }
}
```

---

## Step 3 — Tenant Context Filter (MassTransit)

### `src/Pasukhi.Application/Messaging/ITenantScopedEvent.cs`

```csharp
namespace Pasukhi.Application.Messaging;

/// <summary>
/// Marker for MassTransit events that carry a BusinessId for tenant scoping.
/// The TenantContextFilter reads this before dispatching to the consumer.
/// </summary>
public interface ITenantScopedEvent
{
    Guid BusinessId { get; }
}
```

Update `InboundMessageEvent.cs` to implement the marker:

```csharp
namespace Pasukhi.Application.Messaging;

public record InboundMessageEvent : ITenantScopedEvent
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
    // ExternalAccountId is the page/phone number ID that received the message
    public string ExternalAccountId { get; init; } = string.Empty;
}
```

### `src/Pasukhi.Infrastructure/Messaging/TenantContextFilter.cs`

```csharp
using MassTransit;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;

namespace Pasukhi.Infrastructure.Messaging;

/// <summary>
/// MassTransit consume filter that seeds <see cref="ITenantContext"/> from the
/// incoming event's BusinessId before the consumer runs. Without this, DbContext
/// global filters inside consumers would see BusinessId=Guid.Empty and return nothing.
/// </summary>
public class TenantContextFilter<T> : IFilter<ConsumeContext<T>> where T : class, ITenantScopedEvent
{
    private readonly ITenantContext _tenantContext;

    public TenantContextFilter(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("tenant-context");

    public Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        _tenantContext.SetBusinessId(context.Message.BusinessId);
        return next.Send(context);
    }
}
```

---

## Step 4 — Domain Entities

### `src/Pasukhi.Domain/Entities/Conversation.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class Conversation : TenantEntity
{
    public Guid ChannelConnectionId { get; set; }
    public ChannelConnection ChannelConnection { get; set; } = null!;
    public ChannelType ChannelType { get; set; }
    public string ExternalCustomerId { get; set; } = string.Empty;
    public string? CustomerDisplayName { get; set; }
    public string? CustomerProfilePictureUrl { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Active;
    public bool IsEscalated { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<Escalation> Escalations { get; set; } = new List<Escalation>();
}
```

### `src/Pasukhi.Domain/Entities/Message.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class Message : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public MessageDirection Direction { get; set; }
    public MessageType MessageType { get; set; }
    public string? TextContent { get; set; }
    public string? MediaUrl { get; set; }
    public string? MediaMimeType { get; set; }
    public string ExternalSenderId { get; set; } = string.Empty;
    public string? SenderDisplayName { get; set; }
    public MessageSource Source { get; set; }
    public Guid? MatchedFaqItemId { get; set; }
    public Guid? MatchedRuleId { get; set; }
    public double? AiConfidenceScore { get; set; }
    public Guid? ReplyToMessageId { get; set; }
    public string ExternalMessageId { get; set; } = string.Empty;
    public string? ExternalTimestamp { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Pending;
    public string? RawPayloadJson { get; set; }
}
```

### `src/Pasukhi.Domain/Entities/DailyMetric.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class DailyMetric : TenantEntity
{
    public DateOnly Date { get; set; }
    public ChannelType? ChannelType { get; set; }
    public int TotalInbound { get; set; }
    public int TotalOutbound { get; set; }
    public int FaqReplies { get; set; }
    public int RuleReplies { get; set; }
    public int AiReplies { get; set; }
    public int AiTokensUsed { get; set; }
    public int Escalations { get; set; }
    public int? AvgResponseTimeMs { get; set; }
}
```

Add missing enums if not already present:

### `src/Pasukhi.Domain/Enums/ConversationStatus.cs`
```csharp
namespace Pasukhi.Domain.Enums;
public enum ConversationStatus { Active, Escalated, Resolved, Archived }
```

### `src/Pasukhi.Domain/Enums/MessageDirection.cs`
```csharp
namespace Pasukhi.Domain.Enums;
public enum MessageDirection { Inbound, Outbound }
```

### `src/Pasukhi.Domain/Enums/MessageSource.cs`
```csharp
namespace Pasukhi.Domain.Enums;
public enum MessageSource { Customer, FaqAutoReply, RuleAutoReply, AiAutoReply, OperatorManual }
```

### `src/Pasukhi.Domain/Enums/DeliveryStatus.cs`
```csharp
namespace Pasukhi.Domain.Enums;
public enum DeliveryStatus { Pending, Sent, Delivered, Read, Failed }
```

---

## Step 5 — DbContext: Add New Tables and Indexes

Open `src/Pasukhi.Infrastructure/Data/PasukhiDbContext.cs` and add:

```csharp
public DbSet<Conversation> Conversations => Set<Conversation>();
public DbSet<Message> Messages => Set<Message>();
public DbSet<DailyMetric> DailyMetrics => Set<DailyMetric>();
```

In `OnModelCreating`, add global query filters and indexes:

```csharp
// Conversations
modelBuilder.Entity<Conversation>()
    .HasQueryFilter(c => c.BusinessId == _tenantProvider.BusinessId);

// Messages - unique index for idempotency
modelBuilder.Entity<Message>()
    .HasQueryFilter(m => m.BusinessId == _tenantProvider.BusinessId);
modelBuilder.Entity<Message>()
    .HasIndex(m => new { m.BusinessId, m.ExternalMessageId })
    .IsUnique();

// DailyMetrics
modelBuilder.Entity<DailyMetric>()
    .HasQueryFilter(dm => dm.BusinessId == _tenantProvider.BusinessId);
```

---

## Step 6 — Migration

```bash
cd "C:\Users\piros\OneDrive\Desktop\Pasukhi"
dotnet ef migrations add AddMessageExternalIdUniqueIndex --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
dotnet ef database update --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
```

---

## Step 7 — InboundMessageConsumer (Persistence Only)

Replace the Phase 3 placeholder with the real persistence consumer. The consumer persists the message, bumps the conversation counters, and updates DailyMetrics. It does NOT run FAQ/Rule/AI matching yet — that comes in Phase 7.

### `src/Pasukhi.Infrastructure/Consumers/InboundMessageConsumer.cs`

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Consumers;

/// <summary>
/// Persists one inbound message per webhook event:
///   1. Idempotency check on (BusinessId, ExternalMessageId) backed by a unique index.
///   2. Get-or-create Conversation by (ChannelConnectionId, ExternalCustomerId).
///   3. Insert Message (Direction=Inbound, Source=Customer).
///   4. Bump Conversation.LastMessageAt / UnreadCount.
///   5. Get-or-create DailyMetric row for today/channel and increment TotalInbound.
/// A DbUpdateException from the inbound unique index race is treated as a successful
/// no-op (another worker already persisted it).
/// Tenant context is set by TenantContextFilter before Consume runs.
/// </summary>
public class InboundMessageConsumer : IConsumer<InboundMessageEvent>
{
    private readonly PasukhiDbContext _db;
    private readonly ILogger<InboundMessageConsumer> _logger;

    public InboundMessageConsumer(PasukhiDbContext db, ILogger<InboundMessageConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InboundMessageEvent> context)
    {
        var e = context.Message;
        var ct = context.CancellationToken;

        if (e.BusinessId == Guid.Empty)
        {
            _logger.LogWarning("InboundMessageEvent dropped: empty BusinessId. ExternalMessageId={ExternalMessageId}", e.ExternalMessageId);
            return;
        }

        // 1. Idempotency — skip if already persisted.
        var alreadyExists = await _db.Messages
            .AnyAsync(m => m.BusinessId == e.BusinessId && m.ExternalMessageId == e.ExternalMessageId, ct);
        if (alreadyExists)
        {
            _logger.LogDebug("Duplicate InboundMessage ignored. ExternalMessageId={ExternalMessageId}", e.ExternalMessageId);
            return;
        }

        var channelType = Enum.TryParse<ChannelType>(e.ChannelType, ignoreCase: true, out var ch)
            ? ch
            : (ChannelType?)null;
        if (channelType is null)
        {
            _logger.LogWarning("InboundMessageEvent dropped: unknown ChannelType '{ChannelType}'", e.ChannelType);
            return;
        }

        var messageType = Enum.TryParse<MessageType>(e.MessageType, ignoreCase: true, out var mt)
            ? mt
            : MessageType.Text;

        var timestamp = DateTime.UtcNow;

        // 2. Get-or-create Conversation.
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c =>
                c.ChannelConnectionId == e.ChannelConnectionId &&
                c.ExternalCustomerId == e.ExternalSenderId, ct);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                BusinessId = e.BusinessId,
                ChannelConnectionId = e.ChannelConnectionId,
                ChannelType = channelType.Value,
                ExternalCustomerId = e.ExternalSenderId,
                CustomerDisplayName = e.SenderDisplayName,
                Status = ConversationStatus.Active,
                LastMessageAt = timestamp,
                UnreadCount = 0
            };
            _db.Conversations.Add(conversation);
        }

        // 3. Insert Message.
        var message = new Message
        {
            Id = Guid.NewGuid(),
            BusinessId = e.BusinessId,
            ConversationId = conversation.Id,
            Direction = MessageDirection.Inbound,
            Source = MessageSource.Customer,
            MessageType = messageType,
            TextContent = e.TextContent,
            MediaUrl = e.MediaUrl,
            MediaMimeType = e.MediaMimeType,
            ExternalSenderId = e.ExternalSenderId,
            SenderDisplayName = e.SenderDisplayName,
            ExternalMessageId = e.ExternalMessageId,
            ExternalTimestamp = e.ExternalTimestamp,
            DeliveryStatus = DeliveryStatus.Delivered,
            RawPayloadJson = e.RawPayloadJson
        };
        _db.Messages.Add(message);

        // 4. Bump conversation counters.
        conversation.LastMessageAt = timestamp;
        conversation.UnreadCount += 1;
        if (conversation.Status == ConversationStatus.Resolved || conversation.Status == ConversationStatus.Archived)
        {
            conversation.Status = ConversationStatus.Active;
        }

        // 5. Bump DailyMetric for today/channel.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var metric = await _db.DailyMetrics
            .FirstOrDefaultAsync(m =>
                m.BusinessId == e.BusinessId &&
                m.Date == today &&
                m.ChannelType == channelType, ct);

        if (metric is null)
        {
            metric = new DailyMetric
            {
                Id = Guid.NewGuid(),
                BusinessId = e.BusinessId,
                Date = today,
                ChannelType = channelType,
                TotalInbound = 1
            };
            _db.DailyMetrics.Add(metric);
        }
        else
        {
            metric.TotalInbound += 1;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogInformation(
                "InboundMessage concurrently persisted by another consumer. ExternalMessageId={ExternalMessageId}",
                e.ExternalMessageId);
            foreach (var entry in _db.ChangeTracker.Entries())
                entry.State = EntityState.Detached;
            return;
        }

        _logger.LogInformation(
            "InboundMessage persisted. Business={BusinessId} Channel={ChannelType} Conversation={ConversationId} Message={MessageId}",
            e.BusinessId, channelType, conversation.Id, message.Id);

        // Automation (FAQ/Rule/AI) comes in Phase 7.
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        while (inner is not null)
        {
            var sqlState = inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
            if (sqlState == "23505") return true;
            inner = inner.InnerException;
        }
        return false;
    }
}
```

---

## Step 8 — Register Services in Program.cs

Add inside the service registration section:

```csharp
// Tenant context
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<TenantContext>());
```

Register the middleware after `app.UseAuthentication()` and `app.UseAuthorization()`:

```csharp
app.UseMiddleware<TenantContextMiddleware>();
```

Register the TenantContextFilter inside the existing `AddMassTransit` call:

```csharp
x.AddConsumer<InboundMessageConsumer>();

cfg.ConfigureEndpoints(context);
cfg.UseSendFilter(typeof(TenantContextFilter<>), context);
cfg.UseConsumeFilter(typeof(TenantContextFilter<>), context);
```

---

## Verification

```bash
dotnet build
```

Send a test webhook (see Phase 3 curl example). The log should show:

```
InboundMessage persisted. Business=... Channel=Instagram Conversation=... Message=...
```

A second identical webhook should produce:

```
Duplicate InboundMessage ignored. ExternalMessageId=...
```

---

## Commit

```bash
git add src/ docs/codex/phase-4.md
git commit -m "feat(04): tenant context + message/conversation persistence"
```

---

## What's Next

Phase 5: `docs/codex/phase-5.md` — Outbound message pipeline, channel providers, and `OutboundMessageConsumer`.
