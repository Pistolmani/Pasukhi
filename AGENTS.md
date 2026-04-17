# Pasukhi — Codex Context

## What This Is

Pasukhi (პასუხი — "Answer") is a **multi-tenant B2B SaaS platform** that automates customer replies across Instagram, Facebook Messenger, and WhatsApp for small businesses. An internal admin panel lets operators manage multiple businesses, their FAQ knowledge bases, automation rules, AI prompts, and live conversations.

**Repo:** `C:\Users\piros\OneDrive\Desktop\Pasukhi\`
**Architecture doc:** `docs/ARCHITECTURE.md` — read it before writing any code.

---

## Critical Rules — Read Before Writing a Single Line

1. **Tenant isolation is non-negotiable.** Every entity that belongs to a business MUST extend `TenantEntity`. Every EF Core query for tenant-scoped data goes through a global query filter on `BusinessId`. Never use `.IgnoreQueryFilters()` except in SuperAdmin endpoints.

2. **Webhook handlers must return 200 fast.** The webhook controller does: verify signature → parse → resolve tenant → publish to RabbitMQ → return 200 OK. No database writes. No AI calls. No channel API calls. All processing happens in the queue consumer.

3. **FAQ and rules first, AI last.** The message routing order is: deduplication check → FAQ match → rule match → AI fallback → escalation. AI is called only when nothing else matched. Never call AI on every message.

4. **BusinessId on every queue message.** Every MassTransit message contract includes `BusinessId`. Consumers set `QueueTenantProvider.BusinessId` before doing any database operations.

5. **Follow Dressfield patterns.** Same Clean Architecture layers, same FluentValidation, same record DTOs, same JWT + HttpOnly refresh cookie auth, same Serilog setup, same Mapster for mapping, same direct `DbContext` usage (no repository wrappers unless explicitly specified).

6. **No MediatR.** Services are injected directly. No command/query buses.

7. **No repository pattern by default.** Inject `PasukhiDbContext` directly into services. The only exception: `IConversationRepository` for the complex inbox query.

8. **No Next.js.** The frontend is Vite + React SPA. Not Next.js. No SSR, no `next/image`, no API routes.

9. **Both projects must compile.** After every set of changes: `dotnet build` (backend) and `tsc --noEmit` (frontend). Fix all errors before committing.

10. **Commit format:** `feat(phase-step): description` e.g. `feat(01-03): implement business CRUD`

---

## Tech Stack

### Backend
- **ASP.NET Core 9** Web API
- **PostgreSQL 16** via `Npgsql.EntityFrameworkCore.PostgreSQL`
- **Entity Framework Core 9** — direct DbContext, no repository abstraction (except where noted)
- **ASP.NET Core Identity** — admin user management
- **JWT** — access tokens (15min), HttpOnly refresh cookie (7d)
- **MassTransit** — RabbitMQ abstraction (consumers, retry, dead-letter)
- **FluentValidation** — request validation
- **Mapster** — entity → DTO mapping
- **Serilog** — structured logging
- **Swashbuckle** — Swagger UI in dev

### Frontend
- **Vite + React 19 + TypeScript 5** — SPA, NO Next.js
- **TanStack Query 5** — server state, polling for conversations
- **Zustand 5** — client state (auth, UI)
- **React Hook Form 7 + Zod 4** — forms and validation
- **Tailwind CSS 4 + shadcn/ui** — UI
- **Axios** — HTTP client with JWT interceptor
- **React Router 7** — client-side routing
- **Lucide React** — icons
- **Sonner** — toast notifications

### Infrastructure (local dev)
- **Docker Compose** — PostgreSQL + RabbitMQ
- **ngrok** — webhook tunneling for local Meta integration

---

## Backend Solution Structure

```
Pasukhi/
├── Pasukhi.sln
├── src/
│   ├── Pasukhi.API/           # Controllers, Middleware, Program.cs
│   ├── Pasukhi.Application/   # Services, DTOs, Validators, Interfaces
│   ├── Pasukhi.Domain/        # Entities, Enums (no external dependencies)
│   └── Pasukhi.Infrastructure/ # DbContext, Configurations, Channel adapters, Messaging
├── tests/
│   ├── Pasukhi.UnitTests/
│   └── Pasukhi.IntegrationTests/
├── docker-compose.yml
└── pasukhi-admin/             # Vite React frontend
```

**Dependency direction:** API → Application → Domain ← Infrastructure

---

## Domain Entities

### Base Class — Every Tenant-Scoped Entity

```csharp
// Pasukhi.Domain/Entities/TenantEntity.cs
public abstract class TenantEntity
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### Global Entities (no BusinessId)

```csharp
// Pasukhi.Domain/Entities/Business.cs
public class Business
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;     // unique, url-safe
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<ChannelConnection> ChannelConnections { get; set; } = new List<ChannelConnection>();
}

// Pasukhi.Domain/Entities/AdminUser.cs  (extends IdentityUser)
public class AdminUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid? BusinessId { get; set; }     // null = SuperAdmin; set = Operator
    public Business? Business { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Tenant-Scoped Entities

```csharp
// Pasukhi.Domain/Entities/ChannelConnection.cs
public class ChannelConnection : TenantEntity
{
    public ChannelType ChannelType { get; set; }
    public string ExternalAccountId { get; set; } = string.Empty;  // Meta page/account/phone ID
    public string? ExternalAccountName { get; set; }
    public string AccessToken { get; set; } = string.Empty;        // encrypted
    public string VerifyToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastWebhookAt { get; set; }
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}

// Pasukhi.Domain/Entities/Conversation.cs
public class Conversation : TenantEntity
{
    public Guid ChannelConnectionId { get; set; }
    public ChannelConnection ChannelConnection { get; set; } = null!;
    public ChannelType ChannelType { get; set; }
    public string ExternalCustomerId { get; set; } = string.Empty;
    public string? CustomerDisplayName { get; set; }
    public string? CustomerProfilePictureUrl { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Active;
    public bool IsEscalated { get; set; } = false;
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; } = 0;
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<Escalation> Escalations { get; set; } = new List<Escalation>();
}

// Pasukhi.Domain/Entities/Message.cs
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
    public string ExternalMessageId { get; set; } = string.Empty;
    public string? ExternalTimestamp { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Pending;
    public string? RawPayloadJson { get; set; }
}

// Pasukhi.Domain/Entities/FaqItem.cs
public class FaqItem : TenantEntity
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Keywords { get; set; }     // comma-separated
    public int MatchCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

// Pasukhi.Domain/Entities/AutomationRule.cs
public class AutomationRule : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; } = 0;   // lower = higher priority
    public TriggerType TriggerType { get; set; }
    public string TriggerValue { get; set; } = string.Empty;
    public ActionType ActionType { get; set; }
    public string ActionValue { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int MatchCount { get; set; } = 0;
}

// Pasukhi.Domain/Entities/Escalation.cs
public class Escalation : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public EscalationReason Reason { get; set; }
    public string? Notes { get; set; }
    public string? AiRejectedResponse { get; set; }
    public bool IsResolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByUserId { get; set; }
}

// Pasukhi.Domain/Entities/BusinessPrompt.cs
public class BusinessPrompt : TenantEntity
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string ToneDescription { get; set; } = "professional and friendly";
    public string EscalationMessage { get; set; } = "Let me connect you with our team.";
    public int MaxAiTokensPerDay { get; set; } = 50000;
    public double AiConfidenceThreshold { get; set; } = 0.7;
    public double FaqConfidenceThreshold { get; set; } = 0.85;
    public bool IsAiEnabled { get; set; } = false;
    public DateTime UpdatedAt { get; set; }
}
```

### Enums

```csharp
// Pasukhi.Domain/Enums/
public enum ChannelType       { Instagram = 0, Messenger = 1, WhatsApp = 2 }
public enum MessageDirection  { Inbound = 0, Outbound = 1 }
public enum MessageType       { Text = 0, Image = 1, Video = 2, Audio = 3, File = 4, Sticker = 5, StoryReply = 6, StoryMention = 7, Reaction = 8 }
public enum MessageSource     { Customer = 0, FaqAutoReply = 1, RuleAutoReply = 2, AiAutoReply = 3, OperatorManual = 4 }
public enum ConversationStatus { Active = 0, Escalated = 1, Resolved = 2, Archived = 3 }
public enum DeliveryStatus    { Pending = 0, Sent = 1, Delivered = 2, Read = 3, Failed = 4 }
public enum EscalationReason  { NoMatch = 0, LowAiConfidence = 1, SafetyCheckFailed = 2, CustomerRequested = 3, OperatorTriggered = 4 }
public enum TriggerType       { Keyword = 0, Regex = 1, MessageType = 2, TimeOfDay = 3 }
public enum ActionType        { SendReply = 0, TagConversation = 1, Escalate = 2 }
public enum AdminRole         { SuperAdmin = 0, Operator = 1 }
```

---

## DbContext Pattern

```csharp
// Pasukhi.Infrastructure/Data/PasukhiDbContext.cs
public class PasukhiDbContext : IdentityDbContext<AdminUser>
{
    private readonly ITenantProvider _tenantProvider;

    public PasukhiDbContext(DbContextOptions<PasukhiDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<ChannelConnection> ChannelConnections => Set<ChannelConnection>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<FaqItem> FaqItems => Set<FaqItem>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<Escalation> Escalations => Set<Escalation>();
    public DbSet<BusinessPrompt> BusinessPrompts => Set<BusinessPrompt>();
    public DbSet<BusinessSetting> BusinessSettings => Set<BusinessSetting>();
    public DbSet<DailyMetric> DailyMetrics => Set<DailyMetric>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // TENANT ISOLATION — global query filters on every tenant-scoped entity
        var businessId = _tenantProvider.BusinessId;
        builder.Entity<ChannelConnection>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<Conversation>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<Message>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<FaqItem>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<AutomationRule>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<Escalation>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<BusinessPrompt>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<BusinessSetting>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<DailyMetric>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);

        // Apply IEntityTypeConfiguration files
        builder.ApplyConfigurationsFromAssembly(typeof(PasukhiDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-set audit fields
        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    if (entry.Entity.BusinessId == Guid.Empty)
                        entry.Entity.BusinessId = _tenantProvider.BusinessId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

---

## Tenant Provider

```csharp
// Pasukhi.Application/Interfaces/ITenantProvider.cs
public interface ITenantProvider
{
    Guid BusinessId { get; }
}

// Pasukhi.Infrastructure/Tenant/HttpTenantProvider.cs
// Used for API requests — reads BusinessId from JWT claim
public class HttpTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid BusinessId =>
        Guid.TryParse(
            _httpContextAccessor.HttpContext?.User.FindFirst("BusinessId")?.Value,
            out var id) ? id : Guid.Empty;
}

// Pasukhi.Infrastructure/Messaging/QueueTenantProvider.cs
// Used by MassTransit consumers — manually set before processing
public class QueueTenantProvider : ITenantProvider
{
    public Guid BusinessId { get; set; }
}
```

---

## Key Interfaces

```csharp
// Pasukhi.Application/Interfaces/IChannelProvider.cs
public interface IChannelProvider
{
    ChannelType ChannelType { get; }
    Task<SendResult> SendTextAsync(ChannelConnection connection, string recipientId, string text);
    Task<SendResult> SendMediaAsync(ChannelConnection connection, string recipientId, string mediaUrl, MessageType mediaType);
}

// Pasukhi.Application/Interfaces/IWebhookVerifier.cs
public interface IWebhookVerifier
{
    bool VerifySignature(string payload, string signatureHeader, string appSecret);
    bool VerifySubscription(string mode, string verifyToken, string expectedToken);
}

// Pasukhi.Application/Interfaces/IWebhookParser.cs
public interface IWebhookParser
{
    ChannelType ChannelType { get; }
    IEnumerable<ParsedInboundMessage> Parse(string rawPayload);
}

// Pasukhi.Application/Interfaces/IMessageSender.cs
public interface IMessageSender
{
    Task<SendResult> SendAsync(Guid conversationId, string text, MessageSource source,
        Guid? matchedFaqItemId = null, Guid? matchedRuleId = null, double? aiConfidenceScore = null);
}

// Pasukhi.Application/Interfaces/IAiService.cs
public interface IAiService
{
    Task<AiResponse> GenerateReplyAsync(string systemPrompt, IEnumerable<AiMessage> conversationHistory, string inboundMessage);
}

// Pasukhi.Application/Interfaces/IFaqMatcher.cs
public interface IFaqMatcher
{
    Task<FaqMatchResult?> FindMatchAsync(Guid businessId, string inboundText);
}

// Pasukhi.Application/Interfaces/IRuleMatcher.cs
public interface IRuleMatcher
{
    Task<RuleMatchResult?> FindMatchAsync(Guid businessId, string inboundText, MessageType messageType);
}

// DTOs used by interfaces
public record ParsedInboundMessage(
    ChannelType ChannelType,
    string ExternalAccountId,
    string ExternalSenderId,
    string ExternalMessageId,
    string? SenderDisplayName,
    MessageType MessageType,
    string? TextContent,
    string? MediaUrl,
    string? MediaMimeType,
    string? RawTimestamp,
    string RawPayloadJson
);

public record SendResult(bool Success, string? ExternalMessageId, string? Error);
public record FaqMatchResult(FaqItem FaqItem, double Confidence);
public record RuleMatchResult(AutomationRule Rule);
public record AiResponse(string Text, double Confidence, bool PassedSafetyCheck, string? RejectedReason);
public record AiMessage(string Role, string Content); // Role: "user" | "assistant"
```

---

## Queue Message Contracts

```csharp
// Pasukhi.Infrastructure/Messaging/Contracts/InboundMessageReceived.cs
public record InboundMessageReceived
{
    public Guid BusinessId { get; init; }
    public Guid ChannelConnectionId { get; init; }
    public ChannelType ChannelType { get; init; }
    public string ExternalAccountId { get; init; } = string.Empty;
    public string ExternalSenderId { get; init; } = string.Empty;
    public string ExternalMessageId { get; init; } = string.Empty;
    public string? SenderDisplayName { get; init; }
    public MessageType MessageType { get; init; }
    public string? TextContent { get; init; }
    public string? MediaUrl { get; init; }
    public string? MediaMimeType { get; init; }
    public string? RawTimestamp { get; init; }
    public string RawPayloadJson { get; init; } = string.Empty;
    public DateTime ReceivedAtUtc { get; init; }
}

// Pasukhi.Infrastructure/Messaging/Contracts/OutboundMessageReady.cs
public record OutboundMessageReady
{
    public Guid BusinessId { get; init; }
    public Guid ConversationId { get; init; }
    public Guid ChannelConnectionId { get; init; }
    public ChannelType ChannelType { get; init; }
    public string RecipientExternalId { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public MessageSource Source { get; init; }
    public Guid? MatchedFaqItemId { get; init; }
    public Guid? MatchedRuleId { get; init; }
    public double? AiConfidenceScore { get; init; }
}
```

---

## Message Processing Pipeline (InboundMessageConsumer)

The consumer does this in order:

```
1. Set QueueTenantProvider.BusinessId from message.BusinessId
2. Check idempotency: if ExternalMessageId exists in DB → skip (already processed)
3. Lookup ChannelConnection by ChannelConnectionId
4. GetOrCreate Conversation (by ChannelConnectionId + ExternalSenderId)
5. Save inbound Message entity (Direction = Inbound, Source = Customer)
6. If message has no TextContent → skip auto-reply, mark conversation updated, done
7. Try FaqMatcher.FindMatchAsync(BusinessId, TextContent)
   → If match (confidence >= threshold): publish OutboundMessageReady (Source = FaqAutoReply)
8. Else try RuleMatcher.FindMatchAsync(BusinessId, TextContent, MessageType)
   → If match: execute rule action
     - ActionType.SendReply: publish OutboundMessageReady (Source = RuleAutoReply)
     - ActionType.Escalate: create Escalation entity
9. Else: Load BusinessPrompt, check IsAiEnabled
   → If AI disabled: create Escalation (Reason = NoMatch)
   → If AI enabled: call AiService.GenerateReplyAsync(...)
     - If response.PassedSafetyCheck and confidence >= threshold:
         publish OutboundMessageReady (Source = AiAutoReply, AiConfidenceScore)
     - Else: save AiRejectedResponse, create Escalation
10. Update Conversation.LastMessageAt, UnreadCount
11. Update DailyMetrics
```

---

## Webhook Controller Pattern

```csharp
// The GET endpoint handles Meta's webhook subscription verification
[HttpGet("instagram")]
public IActionResult VerifyInstagramWebhook(
    [FromQuery(Name = "hub.mode")] string mode,
    [FromQuery(Name = "hub.verify_token")] string verifyToken,
    [FromQuery(Name = "hub.challenge")] string challenge)
{
    // Find the channel connection with this verifyToken
    // Call _verifier.VerifySubscription(mode, verifyToken, expectedToken)
    // If valid: return Content(challenge)
    // If invalid: return Forbid()
}

// The POST endpoint receives all messages
[HttpPost("instagram")]
public async Task<IActionResult> ReceiveInstagramWebhook()
{
    // 1. Read raw body as string
    // 2. Get X-Hub-Signature-256 header
    // 3. _verifier.VerifySignature(rawBody, signatureHeader, appSecret) — if fails: return Forbid()
    // 4. Parse messages: _instagramParser.Parse(rawBody)
    // 5. For each parsed message:
    //    a. Resolve tenant: lookup ChannelConnection by ExternalAccountId
    //    b. Publish InboundMessageReceived to RabbitMQ
    // 6. return Ok()  ← ALWAYS return 200. Never let Meta see errors.
}
```

---

## MassTransit Configuration (Program.cs)

```csharp
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<InboundMessageConsumer>();
    x.AddConsumer<OutboundMessageConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"]!, "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });

        cfg.ReceiveEndpoint("inbound-message-received", e =>
        {
            e.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(60)));
            e.ConfigureConsumer<InboundMessageConsumer>(context);
        });

        cfg.ReceiveEndpoint("outbound-message-ready", e =>
        {
            e.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(30)));
            e.ConfigureConsumer<OutboundMessageConsumer>(context);
        });
    });
});
```

---

## appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Jwt": {
    "Secret": "",
    "Issuer": "https://api.pasukhi.ge",
    "Audience": "https://admin.pasukhi.ge",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Username": "guest",
    "Password": "guest"
  },
  "Meta": {
    "AppSecret": "",
    "GraphApiVersion": "v21.0"
  },
  "AI": {
    "Provider": "OpenAI",
    "ApiKey": "",
    "Model": "gpt-4o-mini",
    "MaxTokens": 500,
    "Temperature": 0.3
  },
  "Cors": {
    "Origins": ["http://localhost:5173"]
  }
}
```

```json
// appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=pasukhi_dev;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Secret": "pasukhi-dev-secret-key-must-be-at-least-32-characters-long"
  }
}
```

---

## docker-compose.yml

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: pasukhi_dev
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  rabbitmq:
    image: rabbitmq:3.13-management
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest
    ports:
      - "5672:5672"    # AMQP
      - "15672:15672"  # Management UI

volumes:
  postgres_data:
```

---

## Frontend Structure

```
pasukhi-admin/
├── src/
│   ├── main.tsx
│   ├── App.tsx                      # Router + QueryClient + Toaster
│   ├── api/
│   │   ├── client.ts                # Axios instance, JWT interceptor, refresh logic
│   │   ├── auth.ts
│   │   ├── businesses.ts
│   │   ├── channels.ts
│   │   ├── conversations.ts
│   │   ├── faqs.ts
│   │   ├── rules.ts
│   │   ├── escalations.ts
│   │   ├── analytics.ts
│   │   └── settings.ts
│   ├── components/
│   │   ├── ui/                      # shadcn/ui primitives
│   │   ├── layout/
│   │   │   ├── app-layout.tsx
│   │   │   ├── sidebar.tsx
│   │   │   └── header.tsx
│   │   ├── conversations/
│   │   │   ├── conversation-list.tsx
│   │   │   ├── conversation-detail.tsx
│   │   │   ├── message-bubble.tsx
│   │   │   └── reply-composer.tsx
│   │   ├── faqs/
│   │   │   ├── faq-list.tsx
│   │   │   └── faq-form.tsx
│   │   ├── rules/
│   │   │   ├── rule-list.tsx
│   │   │   └── rule-form.tsx
│   │   ├── escalations/
│   │   │   └── escalation-queue.tsx
│   │   └── shared/
│   │       ├── data-table.tsx
│   │       ├── confirm-dialog.tsx
│   │       ├── loading-spinner.tsx
│   │       └── empty-state.tsx
│   ├── features/
│   │   ├── auth/login-page.tsx
│   │   ├── dashboard/dashboard-page.tsx
│   │   ├── conversations/conversations-page.tsx
│   │   ├── faqs/faqs-page.tsx
│   │   ├── rules/rules-page.tsx
│   │   ├── escalations/escalations-page.tsx
│   │   ├── channels/channels-page.tsx
│   │   ├── settings/business-settings-page.tsx
│   │   └── businesses/businesses-page.tsx   # SuperAdmin only
│   ├── hooks/
│   │   ├── use-auth.ts
│   │   ├── use-conversations.ts
│   │   ├── use-faqs.ts
│   │   └── use-escalations.ts
│   ├── stores/
│   │   ├── auth-store.ts            # user, accessToken
│   │   └── ui-store.ts              # sidebar state
│   ├── types/
│   │   ├── auth.ts
│   │   ├── business.ts
│   │   ├── channel.ts
│   │   ├── conversation.ts
│   │   ├── message.ts
│   │   ├── faq.ts
│   │   ├── rule.ts
│   │   ├── escalation.ts
│   │   └── analytics.ts
│   ├── schemas/
│   │   ├── auth-schemas.ts
│   │   ├── faq-schemas.ts
│   │   ├── rule-schemas.ts
│   │   └── channel-schemas.ts
│   └── lib/
│       ├── utils.ts                 # cn(), formatDate()
│       └── constants.ts             # VITE_API_URL, poll intervals
```

### Routing

```typescript
// App.tsx — route structure
<Routes>
  <Route path="/login" element={<LoginPage />} />
  <Route element={<AuthGuard />}>
    <Route element={<AppLayout />}>
      <Route index element={<DashboardPage />} />
      <Route path="conversations" element={<ConversationsPage />} />
      <Route path="conversations/:id" element={<ConversationsPage />} />
      <Route path="escalations" element={<EscalationsPage />} />
      <Route path="faqs" element={<FaqsPage />} />
      <Route path="faqs/:id" element={<FaqEditPage />} />
      <Route path="rules" element={<RulesPage />} />
      <Route path="channels" element={<ChannelsPage />} />
      <Route path="settings" element={<BusinessSettingsPage />} />
      <Route path="settings/ai" element={<AiPromptPage />} />
      <Route element={<SuperAdminGuard />}>
        <Route path="businesses" element={<BusinessesPage />} />
      </Route>
    </Route>
  </Route>
</Routes>
```

### Polling for Conversations

```typescript
// hooks/use-conversations.ts
export function useConversations(filters: ConversationFilters) {
  return useQuery({
    queryKey: ['conversations', filters],
    queryFn: () => conversationsApi.list(filters),
    refetchInterval: 5000,  // poll every 5s
  });
}

export function useConversation(id: string) {
  return useQuery({
    queryKey: ['conversations', id],
    queryFn: () => conversationsApi.getById(id),
    refetchInterval: 3000,  // poll more frequently for active conversation
  });
}
```

### API Client (auth interceptor)

```typescript
// api/client.ts — same pattern as Dressfield
const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000',
  withCredentials: true,
});

api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    if (error.response?.status === 401 && !error.config._retry) {
      error.config._retry = true;
      try {
        const { data } = await axios.post(
          `${api.defaults.baseURL}/api/auth/refresh`,
          {},
          { withCredentials: true }
        );
        useAuthStore.getState().setAuth(data.user, data.accessToken);
        error.config.headers.Authorization = `Bearer ${data.accessToken}`;
        return api(error.config);
      } catch {
        useAuthStore.getState().clearAuth();
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);
```

---

## API Endpoints Reference

### Auth
```
POST /api/auth/login           body: { email, password } → { accessToken, user }
POST /api/auth/refresh         (cookie) → { accessToken, user }
POST /api/auth/logout          → 204
GET  /api/auth/me              → AdminUserDto
```

### Businesses (SuperAdmin only)
```
GET    /api/businesses         → BusinessDto[]
GET    /api/businesses/{id}    → BusinessDto
POST   /api/businesses         body: CreateBusinessRequest
PUT    /api/businesses/{id}    body: UpdateBusinessRequest
DELETE /api/businesses/{id}    → 204
```

### Channel Connections
```
GET    /api/channels                → ChannelConnectionDto[]
POST   /api/channels                body: CreateChannelRequest
PUT    /api/channels/{id}           body: UpdateChannelRequest
DELETE /api/channels/{id}           → 204
```

### Webhooks (public, no auth)
```
GET  /api/webhooks/instagram    query: hub.mode, hub.verify_token, hub.challenge
POST /api/webhooks/instagram    body: raw Meta payload
GET  /api/webhooks/messenger    (same pattern)
POST /api/webhooks/messenger
GET  /api/webhooks/whatsapp
POST /api/webhooks/whatsapp
```

### Conversations
```
GET  /api/conversations                         → ConversationListDto[]  (filters: status, channel, escalated, search)
GET  /api/conversations/{id}                    → ConversationDto
GET  /api/conversations/{id}/messages           → MessageDto[]
POST /api/conversations/{id}/messages           body: { textContent } → MessageDto  (manual reply)
POST /api/conversations/{id}/escalate           body: { reason, notes }
POST /api/conversations/{id}/resolve            → 204
```

### FAQs
```
GET    /api/faqs                → FaqItemDto[]
POST   /api/faqs                body: CreateFaqRequest
PUT    /api/faqs/{id}           body: UpdateFaqRequest
DELETE /api/faqs/{id}           → 204
```

### Automation Rules
```
GET    /api/rules               → AutomationRuleDto[]
POST   /api/rules               body: CreateRuleRequest
PUT    /api/rules/{id}          body: UpdateRuleRequest
PUT    /api/rules/priorities    body: { ids: Guid[] }  (reorder)
DELETE /api/rules/{id}          → 204
```

### Escalations
```
GET  /api/escalations           → EscalationDto[]  (filters: isResolved)
GET  /api/escalations/{id}      → EscalationDto
POST /api/escalations/{id}/resolve   body: { notes }
```

### Settings
```
GET  /api/settings              → BusinessSettingsDto
PUT  /api/settings              body: BusinessSettingsDto
GET  /api/settings/prompt       → BusinessPromptDto
PUT  /api/settings/prompt       body: BusinessPromptDto
```

### Analytics
```
GET /api/analytics/dashboard    query: days=7   → DashboardDto
```

---

## Naming Conventions

- **Entities:** PascalCase noun (`FaqItem`, `AutomationRule`, `ChannelConnection`)
- **DTOs:** `{Entity}Dto` (read), `Create{Entity}Request`, `Update{Entity}Request`
- **Services:** `I{Domain}Service` interface → `{Domain}Service` implementation
- **Consumers:** `{Name}Consumer` in `Pasukhi.Infrastructure/Messaging/Consumers/`
- **Channel providers:** `{Channel}ChannelProvider` in `Pasukhi.Infrastructure/Channels/`
- **Validators:** `{Request}Validator` alongside the DTO
- **Migrations:** auto-generated via `dotnet ef migrations add {Name}`
- **Commits:** `feat(phase-step): description` e.g. `feat(01-02): domain entities and DbContext`

---

## Local Dev Setup

```bash
# Start infrastructure
docker compose up -d

# Backend
cd src/Pasukhi.API
dotnet run

# Frontend
cd pasukhi-admin
npm run dev

# Tunnel for webhooks (after setting up Meta App)
ngrok http 5000 --domain your-static-domain.ngrok-free.app
```

---

## Build Verification

After any changes:

```bash
# Backend
dotnet build

# Frontend
cd pasukhi-admin && npx tsc --noEmit
```

Both must pass with zero errors before committing.

---

## What Has Been Built (Current State)

- [x] `docs/ARCHITECTURE.md` — complete 22-section architecture blueprint
- [ ] Everything else — not yet implemented

Next task: **Phase 0 scaffolding** (see `docs/ARCHITECTURE.md` Section 21, Phase 0).
