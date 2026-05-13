# Codex Task — Phase 7: Full Auto-Reply Pipeline (FAQ → Rule → Escalate)

> Read `AGENTS.md` first. Phases 0–6 must be complete before starting this.

## Goal

By the end of this phase:
- After persisting an inbound message, `InboundMessageConsumer` runs the FAQ → Rule → Escalation pipeline
- An `ISettingsService` interface exists with a default implementation (real DB-backed implementation comes in Phase 9)
- Auto-reply is skipped when `AutoReplyEnabled` is false
- FAQ matches produce an outbound message published to `OutboundMessageReadyEvent`
- Rule `SendReply` matches do the same; rule `Escalate` matches create an `Escalation`
- If nothing matches, an `Escalation` is created (NoMatch reason)
- A unique index on `(BusinessId, ReplyToMessageId, Source)` prevents duplicate auto-replies
- Outbound messages are persisted as `Pending` before publishing and updated to `Sent`/`Failed` by `OutboundMessageConsumer`

---

## Repo root

`C:\Users\piros\OneDrive\Desktop\Pasukhi\`

---

## Step 1 — ISettingsService (Stub)

The consumer needs to check `AutoReplyEnabled`. Create the interface and a simple stub that returns defaults. The real settings service backed by the database comes in Phase 9 and replaces this stub.

### `src/Pasukhi.Application/Interfaces/ISettingsService.cs`

```csharp
using Pasukhi.Application.DTOs.Settings;

namespace Pasukhi.Application.Interfaces;

public interface ISettingsService
{
    Task<BusinessSettingsDto> GetAsync(CancellationToken ct = default);
    Task<BusinessSettingsDto> UpdateAsync(UpdateBusinessSettingsRequest request, CancellationToken ct = default);
}
```

### `src/Pasukhi.Application/DTOs/Settings/SettingsDtos.cs`

```csharp
namespace Pasukhi.Application.DTOs.Settings;

public static class SettingKeys
{
    public const string AutoReplyEnabled = "auto_reply_enabled";
    public const string WorkingHoursEnabled = "working_hours_enabled";
    public const string WorkingHoursStart = "working_hours_start";
    public const string WorkingHoursEnd = "working_hours_end";
    public const string Timezone = "timezone";
}

public record BusinessSettingsDto(
    bool AutoReplyEnabled,
    bool WorkingHoursEnabled,
    string WorkingHoursStart,
    string WorkingHoursEnd,
    string Timezone);

public record UpdateBusinessSettingsRequest(
    bool AutoReplyEnabled,
    bool WorkingHoursEnabled,
    string WorkingHoursStart,
    string WorkingHoursEnd,
    string Timezone);
```

### `src/Pasukhi.Infrastructure/Services/DefaultSettingsService.cs`

A stub that always returns defaults. Phase 9 replaces this with the real DB-backed implementation.

```csharp
using Pasukhi.Application.DTOs.Settings;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Services;

public class DefaultSettingsService : ISettingsService
{
    private static readonly BusinessSettingsDto Defaults = new(
        AutoReplyEnabled: true,
        WorkingHoursEnabled: false,
        WorkingHoursStart: "09:00",
        WorkingHoursEnd: "18:00",
        Timezone: "Asia/Tbilisi");

    public Task<BusinessSettingsDto> GetAsync(CancellationToken ct = default) =>
        Task.FromResult(Defaults);

    public Task<BusinessSettingsDto> UpdateAsync(UpdateBusinessSettingsRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException("Settings storage is not yet implemented.");
}
```

---

## Step 2 — Migration: Reply Correlation Index

Add the index that prevents duplicate auto-replies for the same inbound message:

```bash
dotnet ef migrations add AddMessageReplyCorrelation --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
```

Verify the migration adds:

```csharp
migrationBuilder.AddColumn<Guid>(
    name: "ReplyToMessageId",
    table: "Messages",
    type: "uuid",
    nullable: true);

migrationBuilder.CreateIndex(
    name: "IX_Messages_BusinessId_ReplyToMessageId_Source",
    table: "Messages",
    columns: new[] { "BusinessId", "ReplyToMessageId", "Source" },
    unique: true,
    filter: "\"ReplyToMessageId\" IS NOT NULL");
```

Apply:

```bash
dotnet ef database update --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
```

---

## Step 3 — Complete InboundMessageConsumer

Replace the Phase 4 persistence-only consumer with the full automation pipeline. The constructor adds `IFaqMatcher`, `IRuleMatcher`, `ISettingsService`, and `IPublishEndpoint`.

### `src/Pasukhi.Infrastructure/Consumers/InboundMessageConsumer.cs`

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pasukhi.Application.DTOs.Settings;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Consumers;

public class InboundMessageConsumer : IConsumer<InboundMessageEvent>
{
    private static readonly IReadOnlyDictionary<string, string> IanaToWindowsTimeZoneIds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Asia/Tbilisi"] = "Georgian Standard Time",
            ["Etc/UTC"] = "UTC",
            ["UTC"] = "UTC",
            ["Europe/Berlin"] = "W. Europe Standard Time",
            ["Europe/London"] = "GMT Standard Time",
            ["America/New_York"] = "Eastern Standard Time",
            ["America/Los_Angeles"] = "Pacific Standard Time"
        };

    private readonly PasukhiDbContext _db;
    private readonly IFaqMatcher _faqMatcher;
    private readonly IRuleMatcher _ruleMatcher;
    private readonly ISettingsService _settingsService;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<InboundMessageConsumer> _logger;

    public InboundMessageConsumer(
        PasukhiDbContext db,
        IFaqMatcher faqMatcher,
        IRuleMatcher ruleMatcher,
        ISettingsService settingsService,
        IPublishEndpoint bus,
        ILogger<InboundMessageConsumer> logger)
    {
        _db = db;
        _faqMatcher = faqMatcher;
        _ruleMatcher = ruleMatcher;
        _settingsService = settingsService;
        _bus = bus;
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

        var alreadyExists = await _db.Messages
            .AnyAsync(m => m.BusinessId == e.BusinessId && m.ExternalMessageId == e.ExternalMessageId, ct);
        if (alreadyExists)
        {
            _logger.LogDebug("Duplicate InboundMessage ignored. ExternalMessageId={ExternalMessageId}", e.ExternalMessageId);
            return;
        }

        var channelType = Enum.TryParse<ChannelType>(e.ChannelType, ignoreCase: true, out var ch) ? ch : (ChannelType?)null;
        if (channelType is null)
        {
            _logger.LogWarning("InboundMessageEvent dropped: unknown ChannelType '{ChannelType}'", e.ChannelType);
            return;
        }

        var messageType = Enum.TryParse<MessageType>(e.MessageType, ignoreCase: true, out var mt) ? mt : MessageType.Text;
        var timestamp = DateTime.UtcNow;

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

        conversation.LastMessageAt = timestamp;
        conversation.UnreadCount += 1;
        if (conversation.Status == ConversationStatus.Resolved || conversation.Status == ConversationStatus.Archived)
            conversation.Status = ConversationStatus.Active;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var metric = await _db.DailyMetrics
            .FirstOrDefaultAsync(m => m.BusinessId == e.BusinessId && m.Date == today && m.ChannelType == channelType, ct);

        if (metric is null)
        {
            metric = new DailyMetric { Id = Guid.NewGuid(), BusinessId = e.BusinessId, Date = today, ChannelType = channelType, TotalInbound = 1 };
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
            _logger.LogInformation("InboundMessage concurrently persisted by another consumer. ExternalMessageId={ExternalMessageId}", e.ExternalMessageId);
            foreach (var entry in _db.ChangeTracker.Entries()) entry.State = EntityState.Detached;
            return;
        }

        _logger.LogInformation(
            "InboundMessage persisted. Business={BusinessId} Channel={ChannelType} Conversation={ConversationId} Message={MessageId}",
            e.BusinessId, channelType, conversation.Id, message.Id);

        if (message.MessageType != MessageType.Text || string.IsNullOrWhiteSpace(message.TextContent))
            return;

        var settings = await _settingsService.GetAsync(ct);
        if (!settings.AutoReplyEnabled)
        {
            _logger.LogInformation("Skipping automation: auto-reply is disabled. Message={MessageId}", message.Id);
            return;
        }

        if (settings.WorkingHoursEnabled && !IsWithinWorkingHours(settings, DateTime.UtcNow))
        {
            _logger.LogInformation("Skipping automation: outside working hours. Message={MessageId}", message.Id);
            return;
        }

        if (await TryCreateFaqAutoReplyAsync(e, conversation, message, channelType.Value, ct)) return;
        if (await TryApplyRuleAsync(e, conversation, message, channelType.Value, ct)) return;

        await CreateEscalationAsync(conversation, message, channelType.Value, EscalationReason.NoMatch, "No FAQ or rule matched.", null, ct);
    }

    private async Task<bool> TryCreateFaqAutoReplyAsync(
        InboundMessageEvent inboundEvent,
        Conversation conversation,
        Message inboundMessage,
        ChannelType channelType,
        CancellationToken ct)
    {
        var match = await _faqMatcher.FindBestMatchAsync(inboundMessage.BusinessId, inboundMessage.TextContent!, ct);
        if (match is null)
        {
            _logger.LogInformation("No FAQ match for inbound message {MessageId}.", inboundMessage.Id);
            return false;
        }

        return await TryCreateOutboundAutoReplyAsync(
            inboundEvent, conversation, inboundMessage, channelType,
            MessageSource.FaqAutoReply, match.FaqItem.Answer,
            matchedFaqItemId: match.FaqItem.Id, matchedRuleId: null,
            aiConfidenceScore: null, aiTokensUsed: 0, ct);
    }

    private async Task<bool> TryApplyRuleAsync(
        InboundMessageEvent inboundEvent,
        Conversation conversation,
        Message inboundMessage,
        ChannelType channelType,
        CancellationToken ct)
    {
        var receivedAt = ParseReceivedAt(inboundEvent);
        var matches = await _ruleMatcher.FindMatchesAsync(
            inboundMessage.BusinessId, inboundMessage.TextContent!,
            inboundMessage.MessageType, receivedAt, ct);

        var match = matches.FirstOrDefault();
        if (match is null)
        {
            _logger.LogInformation("No rule match for inbound message {MessageId}.", inboundMessage.Id);
            return false;
        }

        var rule = match.Rule;
        return rule.ActionType switch
        {
            ActionType.SendReply => await TryCreateRuleReplyAsync(inboundEvent, conversation, inboundMessage, channelType, rule, ct),
            ActionType.Escalate => await CreateRuleEscalationAsync(conversation, inboundMessage, rule, channelType, ct),
            ActionType.TagConversation => LogAndReturn(true, $"TagConversation not yet implemented for rule {rule.Id}."),
            _ => LogAndReturn(false, $"Unsupported action type {rule.ActionType} for rule {rule.Id}.")
        };
    }

    private async Task<bool> TryCreateRuleReplyAsync(
        InboundMessageEvent inboundEvent,
        Conversation conversation,
        Message inboundMessage,
        ChannelType channelType,
        AutomationRule rule,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rule.ActionValue))
        {
            _logger.LogWarning("Rule {RuleId} matched but SendReply action is empty.", rule.Id);
            return true;
        }

        return await TryCreateOutboundAutoReplyAsync(
            inboundEvent, conversation, inboundMessage, channelType,
            MessageSource.RuleAutoReply, rule.ActionValue,
            matchedFaqItemId: null, matchedRuleId: rule.Id,
            aiConfidenceScore: null, aiTokensUsed: 0, ct);
    }

    private async Task<bool> CreateRuleEscalationAsync(
        Conversation conversation,
        Message inboundMessage,
        AutomationRule rule,
        ChannelType channelType,
        CancellationToken ct) =>
        await CreateEscalationAsync(
            conversation, inboundMessage, channelType,
            EscalationReason.CustomerRequested,
            string.IsNullOrWhiteSpace(rule.ActionValue) ? $"Escalated by rule: {rule.Name}" : rule.ActionValue,
            null, ct);

    private async Task<bool> TryCreateOutboundAutoReplyAsync(
        InboundMessageEvent inboundEvent,
        Conversation conversation,
        Message inboundMessage,
        ChannelType channelType,
        MessageSource source,
        string text,
        Guid? matchedFaqItemId,
        Guid? matchedRuleId,
        double? aiConfidenceScore,
        int aiTokensUsed,
        CancellationToken ct)
    {
        var existingReply = await _db.Messages.AnyAsync(m =>
            m.BusinessId == inboundMessage.BusinessId &&
            m.ReplyToMessageId == inboundMessage.Id &&
            m.Source == source, ct);
        if (existingReply)
        {
            _logger.LogDebug("{Source} auto-reply already exists for inbound message {MessageId}.", source, inboundMessage.Id);
            return true;
        }

        var outboundId = Guid.NewGuid();
        var outboundMessage = new Message
        {
            Id = outboundId,
            BusinessId = inboundMessage.BusinessId,
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Source = source,
            MessageType = MessageType.Text,
            TextContent = text,
            ExternalSenderId = inboundEvent.ExternalAccountId,
            ExternalMessageId = $"pending:{outboundId}",
            DeliveryStatus = DeliveryStatus.Pending,
            MatchedFaqItemId = matchedFaqItemId,
            MatchedRuleId = matchedRuleId,
            AiConfidenceScore = aiConfidenceScore,
            ReplyToMessageId = inboundMessage.Id
        };

        _db.Messages.Add(outboundMessage);
        conversation.LastMessageAt = DateTime.UtcNow;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var metric = await GetOrCreateMetricAsync(inboundMessage.BusinessId, channelType, today, ct);
        metric.TotalOutbound += 1;
        if (source == MessageSource.FaqAutoReply) metric.FaqReplies += 1;
        else if (source == MessageSource.RuleAutoReply) metric.RuleReplies += 1;
        else if (source == MessageSource.AiAutoReply) { metric.AiReplies += 1; metric.AiTokensUsed += Math.Max(0, aiTokensUsed); }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogInformation("{Source} auto-reply concurrently persisted for inbound message {MessageId}.", source, inboundMessage.Id);
            foreach (var entry in _db.ChangeTracker.Entries()) entry.State = EntityState.Detached;
            return true;
        }

        await _bus.Publish(new OutboundMessageReadyEvent
        {
            BusinessId = outboundMessage.BusinessId,
            MessageId = outboundMessage.Id,
            ConversationId = conversation.Id,
            ChannelConnectionId = conversation.ChannelConnectionId,
            ChannelType = channelType.ToString(),
            ExternalCustomerId = conversation.ExternalCustomerId,
            TextContent = outboundMessage.TextContent
        }, ct);

        _logger.LogInformation(
            "{Source} auto-reply queued. InboundMessage={InboundMessageId} OutboundMessage={OutboundMessageId}",
            source, inboundMessage.Id, outboundId);

        return true;
    }

    private async Task<bool> CreateEscalationAsync(
        Conversation conversation,
        Message inboundMessage,
        ChannelType channelType,
        EscalationReason reason,
        string? notes,
        string? aiRejectedResponse,
        CancellationToken ct)
    {
        var alreadyEscalated = await _db.Escalations.AnyAsync(e =>
            e.BusinessId == inboundMessage.BusinessId &&
            e.ConversationId == conversation.Id &&
            !e.IsResolved, ct);

        if (!alreadyEscalated)
        {
            _db.Escalations.Add(new Escalation
            {
                Id = Guid.NewGuid(),
                BusinessId = inboundMessage.BusinessId,
                ConversationId = conversation.Id,
                Reason = reason,
                Notes = notes,
                AiRejectedResponse = aiRejectedResponse
            });

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var metric = await GetOrCreateMetricAsync(inboundMessage.BusinessId, channelType, today, ct);
            metric.Escalations += 1;
        }

        conversation.IsEscalated = true;
        conversation.Status = ConversationStatus.Escalated;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Conversation escalated. Reason={Reason} InboundMessage={InboundMessageId} Conversation={ConversationId}",
            reason, inboundMessage.Id, conversation.Id);

        return true;
    }

    private async Task<DailyMetric> GetOrCreateMetricAsync(Guid businessId, ChannelType channelType, DateOnly date, CancellationToken ct)
    {
        var metric = await _db.DailyMetrics.FirstOrDefaultAsync(m =>
            m.BusinessId == businessId && m.Date == date && m.ChannelType == channelType, ct);

        if (metric is not null) return metric;

        metric = new DailyMetric { Id = Guid.NewGuid(), BusinessId = businessId, Date = date, ChannelType = channelType };
        _db.DailyMetrics.Add(metric);
        return metric;
    }

    private static DateTimeOffset ParseReceivedAt(InboundMessageEvent e)
    {
        if (long.TryParse(e.ExternalTimestamp, out var unix))
        {
            try { return DateTimeOffset.FromUnixTimeSeconds(unix); }
            catch (ArgumentOutOfRangeException) { return DateTimeOffset.UtcNow; }
        }
        return DateTimeOffset.TryParse(e.ExternalTimestamp, out var parsed) ? parsed : DateTimeOffset.UtcNow;
    }

    private static bool IsWithinWorkingHours(BusinessSettingsDto settings, DateTime utcNow)
    {
        if (!TimeOnly.TryParse(settings.WorkingHoursStart, out var start) ||
            !TimeOnly.TryParse(settings.WorkingHoursEnd, out var end))
            return true;

        var timezone = ResolveTimezone(settings.Timezone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), timezone);
        var current = TimeOnly.FromDateTime(localNow);
        return start <= end ? current >= start && current <= end : current >= start || current <= end;
    }

    private static TimeZoneInfo ResolveTimezone(string timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(timezone); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            if (IanaToWindowsTimeZoneIds.TryGetValue(timezone, out var windowsId))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(windowsId); }
                catch (Exception fallbackEx) when (fallbackEx is TimeZoneNotFoundException or InvalidTimeZoneException)
                { return TimeZoneInfo.Utc; }
            }
        }
        return TimeZoneInfo.Utc;
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

    private bool LogAndReturn(bool value, string message)
    {
        _logger.LogInformation("{Message}", message);
        return value;
    }
}
```

---

## Step 4 — Register Services in Program.cs

```csharp
// Stub settings service (replaced in Phase 9)
builder.Services.AddScoped<ISettingsService, DefaultSettingsService>();
```

---

## Verification

```bash
dotnet build
```

Send an inbound webhook where the text matches a FAQ item. Confirm in logs:
```
InboundMessage persisted. ...
FaqAutoReply auto-reply queued. InboundMessage=... OutboundMessage=...
Outbound message sent. Message=... ExternalMessageId=...
```

Send a message that doesn't match any FAQ or rule. Confirm:
```
Conversation escalated. Reason=NoMatch ...
```

---

## Commit

```bash
git add src/ docs/codex/phase-7.md
git commit -m "feat(07): full auto-reply pipeline — FAQ → rule → escalate"
```

---

## What's Next

Phase 8: `docs/codex/phase-8.md` — AI fallback: Gemini + OpenAI services, prompt builder, safety checker, and `AiController`.
