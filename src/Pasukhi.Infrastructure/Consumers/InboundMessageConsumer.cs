using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeZoneConverter;
using Pasukhi.Application.AI;
using Pasukhi.Application.DTOs.Settings;
using Pasukhi.Application.Interfaces;
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
///   6. After inbound persistence succeeds, attempt FAQ auto-reply for text messages.
/// A DbUpdateException from the inbound unique index race is treated as a successful
/// no-op (another worker already persisted it).
/// Tenant context is set by the background service before ProcessAsync runs.
/// </summary>
public class InboundMessageConsumer
{

    private readonly PasukhiDbContext _db;
    private readonly IFaqMatcher _faqMatcher;
    private readonly IRuleMatcher _ruleMatcher;
    private readonly ISettingsService _settingsService;
    private readonly IAiPromptBuilder _aiPromptBuilder;
    private readonly IAiService _aiService;
    private readonly IAiSafetyChecker _aiSafetyChecker;
    private readonly AiOptions _aiOptions;
    private readonly ChannelWriter<OutboundMessageReadyEvent> _outboundWriter;
    private readonly ILogger<InboundMessageConsumer> _logger;

    public InboundMessageConsumer(
        PasukhiDbContext db,
        IFaqMatcher faqMatcher,
        IRuleMatcher ruleMatcher,
        ISettingsService settingsService,
        IAiPromptBuilder aiPromptBuilder,
        IAiService aiService,
        IAiSafetyChecker aiSafetyChecker,
        IOptions<AiOptions> aiOptions,
        ChannelWriter<OutboundMessageReadyEvent> outboundWriter,
        ILogger<InboundMessageConsumer> logger)
    {
        _db = db;
        _faqMatcher = faqMatcher;
        _ruleMatcher = ruleMatcher;
        _settingsService = settingsService;
        _aiPromptBuilder = aiPromptBuilder;
        _aiService = aiService;
        _aiSafetyChecker = aiSafetyChecker;
        _aiOptions = aiOptions.Value;
        _outboundWriter = outboundWriter;
        _logger = logger;
    }

    public async Task ProcessAsync(InboundMessageEvent e, CancellationToken ct)
    {
        if (e.BusinessId == Guid.Empty)
        {
            _logger.LogWarning("InboundMessageEvent dropped: empty BusinessId. ExternalMessageId={ExternalMessageId}", e.ExternalMessageId);
            return;
        }

        // 1. Idempotency - skip if already persisted.
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
            // Lost an idempotency race with another consumer - another worker already
            // persisted this message. Drop our pending changes and treat as success.
            _logger.LogInformation(
                "InboundMessage concurrently persisted by another consumer. ExternalMessageId={ExternalMessageId}",
                e.ExternalMessageId);
            foreach (var entry in _db.ChangeTracker.Entries())
            {
                entry.State = EntityState.Detached;
            }
            return;
        }

        _logger.LogInformation(
            "InboundMessage persisted. Business={BusinessId} Channel={ChannelType} Conversation={ConversationId} Message={MessageId}",
            e.BusinessId, channelType, conversation.Id, message.Id);

        if (message.MessageType != MessageType.Text || string.IsNullOrWhiteSpace(message.TextContent))
        {
            _logger.LogDebug(
                "Skipping automation for non-text or empty inbound message {MessageId}.",
                message.Id);
            return;
        }

        var settings = await _settingsService.GetAsync(ct);
        if (!settings.AutoReplyEnabled)
        {
            _logger.LogInformation(
                "Skipping automation for inbound message {MessageId}: auto-reply is disabled.",
                message.Id);
            return;
        }

        if (settings.WorkingHoursEnabled && !IsWithinWorkingHours(settings, DateTime.UtcNow))
        {
            _logger.LogInformation(
                "Skipping automation for inbound message {MessageId}: outside working hours.",
                message.Id);
            return;
        }

        if (await TryCreateFaqAutoReplyAsync(e, conversation, message, channelType.Value, ct))
        {
            return;
        }

        if (await TryApplyRuleAsync(e, conversation, message, channelType.Value, ct))
        {
            return;
        }

        await TryApplyAiAsync(e, conversation, message, channelType.Value, ct);
    }

    private async Task<bool> TryCreateFaqAutoReplyAsync(
        InboundMessageEvent inboundEvent,
        Conversation conversation,
        Message inboundMessage,
        ChannelType channelType,
        CancellationToken ct)
    {
        var match = await _faqMatcher.FindBestMatchAsync(
            inboundMessage.BusinessId,
            inboundMessage.TextContent!,
            ct);

        if (match is null)
        {
            _logger.LogInformation(
                "No FAQ match for inbound message {MessageId}. Conversation={ConversationId}",
                inboundMessage.Id,
                conversation.Id);
            return false;
        }

        return await TryCreateOutboundAutoReplyAsync(
            inboundEvent,
            conversation,
            inboundMessage,
            channelType,
            MessageSource.FaqAutoReply,
            match.FaqItem.Answer,
            matchedFaqItemId: match.FaqItem.Id,
            matchedRuleId: null,
            aiConfidenceScore: null,
            aiTokensUsed: 0,
            ct);
    }

    private async Task<bool> TryApplyRuleAsync(
        InboundMessageEvent inboundEvent,
        Conversation conversation,
        Message inboundMessage,
        ChannelType channelType,
        CancellationToken ct)
    {
        var matches = await _ruleMatcher.FindMatchesAsync(
            inboundMessage.BusinessId,
            inboundMessage.TextContent!,
            inboundMessage.MessageType,
            ParseReceivedAt(inboundEvent),
            ct);

        var match = matches.FirstOrDefault();
        if (match is null)
        {
            _logger.LogInformation(
                "No automation rule match for inbound message {MessageId}. Conversation={ConversationId}",
                inboundMessage.Id,
                conversation.Id);
            return false;
        }

        var rule = match.Rule;
        return rule.ActionType switch
        {
            ActionType.SendReply => await TryCreateRuleReplyAsync(
                inboundEvent,
                conversation,
                inboundMessage,
                channelType,
                rule,
                ct),
            ActionType.Escalate => await CreateRuleEscalationAsync(
                conversation,
                inboundMessage,
                rule,
                channelType,
                ct),
            ActionType.TagConversation => LogUnsupportedTagRule(rule, inboundMessage),
            _ => LogUnsupportedRuleAction(rule, inboundMessage)
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
            _logger.LogWarning(
                "Automation rule {RuleId} matched inbound message {MessageId}, but SendReply action is empty.",
                rule.Id,
                inboundMessage.Id);
            return true;
        }

        return await TryCreateOutboundAutoReplyAsync(
            inboundEvent,
            conversation,
            inboundMessage,
            channelType,
            MessageSource.RuleAutoReply,
            rule.ActionValue,
            matchedFaqItemId: null,
            matchedRuleId: rule.Id,
            aiConfidenceScore: null,
            aiTokensUsed: 0,
            ct);
    }

    private async Task<bool> TryApplyAiAsync(
        InboundMessageEvent inboundEvent,
        Conversation conversation,
        Message inboundMessage,
        ChannelType channelType,
        CancellationToken ct)
    {
        var aiContext = await _aiPromptBuilder.BuildAsync(conversation, inboundMessage, channelType, ct);
        if (aiContext is null)
        {
            return await CreateEscalationAsync(
                conversation,
                inboundMessage,
                channelType,
                EscalationReason.NoMatch,
                "No AI prompt is configured for this business.",
                aiRejectedResponse: null,
                ct);
        }

        if (!aiContext.IsAiEnabled)
        {
            return await CreateEscalationAsync(
                conversation,
                inboundMessage,
                channelType,
                EscalationReason.NoMatch,
                "AI fallback is disabled for this business.",
                aiRejectedResponse: null,
                ct);
        }

        var metric = await GetOrCreateMetricAsync(inboundMessage.BusinessId, channelType, ct);
        if (metric.AiTokensUsed + _aiOptions.MaxTokens > aiContext.MaxAiTokensPerDay)
        {
            return await CreateEscalationAsync(
                conversation,
                inboundMessage,
                channelType,
                EscalationReason.NoMatch,
                "AI daily token budget was exceeded.",
                aiRejectedResponse: null,
                ct);
        }

        var result = await _aiService.GenerateReplyAsync(aiContext, ct);
        if (!result.Success)
        {
            return await CreateEscalationAsync(
                conversation,
                inboundMessage,
                channelType,
                EscalationReason.NoMatch,
                result.Error ?? "AI fallback failed.",
                result.ReplyText,
                ct);
        }

        if (result.ConfidenceScore < aiContext.AiConfidenceThreshold)
        {
            return await CreateEscalationAsync(
                conversation,
                inboundMessage,
                channelType,
                EscalationReason.LowAiConfidence,
                $"AI confidence {result.ConfidenceScore:0.00} was below threshold {aiContext.AiConfidenceThreshold:0.00}.",
                result.ReplyText,
                ct);
        }

        if (result.ShouldEscalate)
        {
            return await CreateEscalationAsync(
                conversation,
                inboundMessage,
                channelType,
                EscalationReason.NoMatch,
                result.EscalationReason ?? "AI requested escalation.",
                result.ReplyText,
                ct);
        }

        var safety = await _aiSafetyChecker.ValidateAsync(aiContext, result, ct);
        if (!safety.Passed)
        {
            return await CreateEscalationAsync(
                conversation,
                inboundMessage,
                channelType,
                EscalationReason.SafetyCheckFailed,
                safety.RejectionReason ?? "AI reply failed safety checks.",
                result.ReplyText,
                ct);
        }

        return await TryCreateOutboundAutoReplyAsync(
            inboundEvent,
            conversation,
            inboundMessage,
            channelType,
            MessageSource.AiAutoReply,
            result.ReplyText!,
            matchedFaqItemId: null,
            matchedRuleId: null,
            aiConfidenceScore: result.ConfidenceScore,
            aiTokensUsed: result.TokensUsed,
            ct);
    }

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
            _logger.LogDebug(
                "{Source} auto-reply already exists for inbound message {MessageId}.",
                source,
                inboundMessage.Id);
            return true;
        }

        var outboundMessageId = Guid.NewGuid();
        var outboundMessage = new Message
        {
            Id = outboundMessageId,
            BusinessId = inboundMessage.BusinessId,
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Source = source,
            MessageType = MessageType.Text,
            TextContent = text,
            ExternalSenderId = inboundEvent.ExternalAccountId,
            ExternalMessageId = $"pending:{outboundMessageId}",
            DeliveryStatus = DeliveryStatus.Pending,
            MatchedFaqItemId = matchedFaqItemId,
            MatchedRuleId = matchedRuleId,
            AiConfidenceScore = aiConfidenceScore,
            ReplyToMessageId = inboundMessage.Id
        };

        _db.Messages.Add(outboundMessage);
        conversation.LastMessageAt = DateTime.UtcNow;

        // Use GetOrCreateMetricAsync so the change-tracker check prevents a second
        // DailyMetric entity being created when one was already added earlier in
        // this scope (e.g. by TryApplyAiAsync) but not yet saved to the database.
        var metric = await GetOrCreateMetricAsync(inboundMessage.BusinessId, channelType, ct);
        metric.TotalOutbound += 1;
        if (source == MessageSource.FaqAutoReply)
        {
            metric.FaqReplies += 1;
        }
        else if (source == MessageSource.RuleAutoReply)
        {
            metric.RuleReplies += 1;
        }
        else if (source == MessageSource.AiAutoReply)
        {
            metric.AiReplies += 1;
            metric.AiTokensUsed += Math.Max(0, aiTokensUsed);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogInformation(
                "{Source} auto-reply concurrently persisted by another consumer. InboundMessageId={InboundMessageId}",
                source,
                inboundMessage.Id);
            foreach (var entry in _db.ChangeTracker.Entries())
            {
                entry.State = EntityState.Detached;
            }
            return true;
        }

        await _outboundWriter.WriteAsync(new OutboundMessageReadyEvent
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
            "{Source} auto-reply queued. InboundMessage={InboundMessageId} OutboundMessage={OutboundMessageId} Faq={FaqItemId} Rule={RuleId}",
            source,
            inboundMessage.Id,
            outboundMessage.Id,
            matchedFaqItemId,
            matchedRuleId);

        return true;
    }

    private async Task<bool> CreateRuleEscalationAsync(
        Conversation conversation,
        Message inboundMessage,
        AutomationRule rule,
        ChannelType channelType,
        CancellationToken ct)
    {
        return await CreateEscalationAsync(
            conversation,
            inboundMessage,
            channelType,
            EscalationReason.CustomerRequested,
            string.IsNullOrWhiteSpace(rule.ActionValue)
                ? $"Escalated by automation rule: {rule.Name}"
                : rule.ActionValue,
            aiRejectedResponse: null,
            ct);
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

        var createdEscalation = false;
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
            createdEscalation = true;
        }

        conversation.IsEscalated = true;
        conversation.Status = ConversationStatus.Escalated;

        if (createdEscalation)
        {
            var metric = await GetOrCreateMetricAsync(inboundMessage.BusinessId, channelType, ct);
            metric.Escalations += 1;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Conversation escalated. Reason={Reason} InboundMessage={InboundMessageId} Conversation={ConversationId}",
            reason,
            inboundMessage.Id,
            conversation.Id);

        return true;
    }

    private async Task<DailyMetric> GetOrCreateMetricAsync(
        Guid businessId,
        ChannelType channelType,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Check the change tracker FIRST. If a new DailyMetric for today was already
        // added in this DbContext scope (but not yet saved), a DB query won't find it —
        // which would create a second entity for the same (BusinessId, Date, ChannelType)
        // unique key and cause a constraint violation on SaveChangesAsync.
        var tracked = _db.ChangeTracker
            .Entries<DailyMetric>()
            .FirstOrDefault(e =>
                e.Entity.BusinessId == businessId &&
                e.Entity.Date == today &&
                e.Entity.ChannelType == channelType)
            ?.Entity;

        if (tracked is not null)
        {
            return tracked;
        }

        var metric = await _db.DailyMetrics.FirstOrDefaultAsync(m =>
            m.BusinessId == businessId &&
            m.Date == today &&
            m.ChannelType == channelType, ct);

        if (metric is not null)
        {
            return metric;
        }

        metric = new DailyMetric
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Date = today,
            ChannelType = channelType
        };
        _db.DailyMetrics.Add(metric);
        return metric;
    }

    private bool LogUnsupportedTagRule(AutomationRule rule, Message inboundMessage)
    {
        _logger.LogInformation(
            "Automation rule {RuleId} matched inbound message {MessageId}, but TagConversation is not implemented yet.",
            rule.Id,
            inboundMessage.Id);
        return true;
    }

    private bool LogUnsupportedRuleAction(AutomationRule rule, Message inboundMessage)
    {
        _logger.LogWarning(
            "Automation rule {RuleId} matched inbound message {MessageId}, but action {ActionType} is unsupported.",
            rule.Id,
            inboundMessage.Id,
            rule.ActionType);
        return true;
    }

    private static DateTimeOffset ParseReceivedAt(InboundMessageEvent inboundEvent)
    {
        if (long.TryParse(inboundEvent.ExternalTimestamp, out var unixSeconds))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTimeOffset.UtcNow;
            }
        }

        return DateTimeOffset.TryParse(inboundEvent.ExternalTimestamp, out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;
    }

    private static bool IsWithinWorkingHours(
        BusinessSettingsDto settings,
        DateTime utcNow)
    {
        if (!TimeOnly.TryParse(settings.WorkingHoursStart, out var start) ||
            !TimeOnly.TryParse(settings.WorkingHoursEnd, out var end))
        {
            return true;
        }

        var timezone = ResolveTimezone(settings.Timezone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), timezone);
        var current = TimeOnly.FromDateTime(localNow);

        return start <= end
            ? current >= start && current <= end
            : current >= start || current <= end;
    }

    private static TimeZoneInfo ResolveTimezone(string timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return TimeZoneInfo.Utc;
        }

        // TZConvert handles IANA names (e.g. "Asia/Tbilisi"), Windows names
        // (e.g. "Georgian Standard Time"), and Rails zone names — all in one call.
        // Replaces the previous hardcoded 7-entry IANA→Windows dictionary.
        return TZConvert.TryGetTimeZoneInfo(timezone, out var tz) ? tz : TimeZoneInfo.Utc;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // Npgsql PostgresException.SqlState "23505" = unique_violation.
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
