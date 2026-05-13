# Codex Task — Phase 8: AI Fallback Pipeline

> Read `AGENTS.md` first. Phases 0–7 must be complete before starting this.

## Goal

By the end of this phase:
- When FAQ and rules produce no match, `InboundMessageConsumer` calls Gemini (default) or OpenAI as a fallback
- `BusinessPrompt` entity stores per-tenant AI configuration (system prompt, tone, confidence thresholds, token budget, on/off switch)
- `AiPromptBuilder` constructs AI context from the business prompt, top-scoring FAQ items, and conversation history
- `AiSafetyChecker` validates AI responses for length, uncertainty language, and contact detail hallucinations
- If AI confidence is below threshold, or safety check fails, or AI is disabled, the consumer falls back to creating an Escalation
- Operators configure AI settings via `PUT /api/ai/prompt`
- Daily token usage is tracked in `DailyMetric.AiTokensUsed`

---

## Repo root

`C:\Users\piros\OneDrive\Desktop\Pasukhi\`

---

## Step 1 — BusinessPrompt Entity

### `src/Pasukhi.Domain/Entities/BusinessPrompt.cs`

```csharp
namespace Pasukhi.Domain.Entities;

public class BusinessPrompt : TenantEntity
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string ToneDescription { get; set; } = "professional and friendly";
    public string EscalationMessage { get; set; } = "Let me connect you with our team.";
    public int MaxAiTokensPerDay { get; set; } = 50000;
    public double AiConfidenceThreshold { get; set; } = 0.7;
    public double FaqConfidenceThreshold { get; set; } = 0.85;
    public bool IsAiEnabled { get; set; }
}
```

Add to `PasukhiDbContext`:

```csharp
public DbSet<BusinessPrompt> BusinessPrompts => Set<BusinessPrompt>();
```

In `OnModelCreating`:

```csharp
modelBuilder.Entity<BusinessPrompt>()
    .HasQueryFilter(bp => bp.BusinessId == _tenantProvider.BusinessId);
```

---

## Step 2 — AI Configuration

### `src/Pasukhi.Application/AI/AiOptions.cs`

```csharp
namespace Pasukhi.Application.AI;

public class AiOptions
{
    public string Provider { get; set; } = "Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash-lite";
    public int MaxTokens { get; set; } = 500;
    public double Temperature { get; set; } = 0.3;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public string ReasoningEffort { get; set; } = "low";

    public string GetDefaultModel() =>
        !string.IsNullOrWhiteSpace(Model) ? Model :
        Provider.ToLowerInvariant() switch
        {
            "gemini" or "google" => "gemini-2.0-flash-lite",
            _ => "gpt-5-mini"
        };
}
```

---

## Step 3 — AI Interfaces

### `src/Pasukhi.Application/Interfaces/IAiService.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Interfaces;

public record AiFaqContextItem(Guid Id, string Question, string Answer, string? Keywords);

public record AiMessage(string Role, string Content, DateTime CreatedAt);

public record AiContext(
    Guid BusinessId,
    Guid ConversationId,
    Guid InboundMessageId,
    string BusinessName,
    string? BusinessDescription,
    string SystemPrompt,
    string ToneDescription,
    string EscalationMessage,
    bool IsAiEnabled,
    int MaxAiTokensPerDay,
    double AiConfidenceThreshold,
    ChannelType ChannelType,
    string CustomerDisplayName,
    string InboundMessageText,
    IReadOnlyList<AiFaqContextItem> RelevantFaqs,
    IReadOnlyList<AiMessage> ConversationHistory);

public record AiReplyResult(
    bool Success,
    string? ReplyText,
    double ConfidenceScore,
    bool ShouldEscalate,
    string? EscalationReason,
    int TokensUsed,
    TimeSpan ProcessingTime,
    string? Error = null);

public interface IAiService
{
    Task<AiReplyResult> GenerateReplyAsync(AiContext context, CancellationToken ct = default);
}
```

### `src/Pasukhi.Application/Interfaces/IAiPromptBuilder.cs`

```csharp
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Interfaces;

public interface IAiPromptBuilder
{
    Task<AiContext?> BuildAsync(
        Conversation conversation,
        Message inboundMessage,
        ChannelType channelType,
        CancellationToken ct = default);
}
```

### `src/Pasukhi.Application/Interfaces/IAiSafetyChecker.cs`

```csharp
namespace Pasukhi.Application.Interfaces;

public record AiSafetyResult(bool Passed, string? RejectionReason);

public interface IAiSafetyChecker
{
    Task<AiSafetyResult> ValidateAsync(AiContext context, AiReplyResult result, CancellationToken ct = default);
}
```

---

## Step 4 — AiPromptBuilder

### `src/Pasukhi.Infrastructure/Services/AiPromptBuilder.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Text;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class AiPromptBuilder : IAiPromptBuilder
{
    private const int MaxFaqContextItems = 10;
    private const int MaxHistoryMessages = 10;
    private readonly PasukhiDbContext _db;

    public AiPromptBuilder(PasukhiDbContext db)
    {
        _db = db;
    }

    public async Task<AiContext?> BuildAsync(
        Conversation conversation,
        Message inboundMessage,
        ChannelType channelType,
        CancellationToken ct = default)
    {
        var prompt = await _db.BusinessPrompts
            .FirstOrDefaultAsync(p => p.BusinessId == inboundMessage.BusinessId, ct);
        if (prompt is null) return null;

        var business = await _db.Businesses
            .FirstOrDefaultAsync(b => b.Id == inboundMessage.BusinessId, ct);

        var faqs = await _db.FaqItems
            .Where(f => f.BusinessId == inboundMessage.BusinessId && f.IsActive)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Question)
            .ToListAsync(ct);

        var relevantFaqs = faqs
            .Select(faq => new { Faq = faq, Score = ScoreFaq(faq, inboundMessage.TextContent) })
            .OrderByDescending(x => x.Score).ThenBy(x => x.Faq.SortOrder).ThenBy(x => x.Faq.Question)
            .Take(MaxFaqContextItems)
            .Select(x => new AiFaqContextItem(x.Faq.Id, x.Faq.Question, x.Faq.Answer, x.Faq.Keywords))
            .ToList();

        var history = await _db.Messages
            .Where(m =>
                m.BusinessId == inboundMessage.BusinessId &&
                m.ConversationId == conversation.Id &&
                m.Id != inboundMessage.Id &&
                m.TextContent != null && m.TextContent != string.Empty)
            .OrderByDescending(m => m.CreatedAt)
            .Take(MaxHistoryMessages)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AiMessage(
                m.Direction == MessageDirection.Inbound ? "customer" : "assistant",
                m.TextContent!,
                m.CreatedAt))
            .ToListAsync(ct);

        return new AiContext(
            inboundMessage.BusinessId,
            conversation.Id,
            inboundMessage.Id,
            business?.Name ?? "the business",
            business?.Description,
            prompt.SystemPrompt,
            prompt.ToneDescription,
            prompt.EscalationMessage,
            prompt.IsAiEnabled,
            prompt.MaxAiTokensPerDay,
            prompt.AiConfidenceThreshold,
            channelType,
            conversation.CustomerDisplayName ?? inboundMessage.SenderDisplayName ?? conversation.ExternalCustomerId,
            inboundMessage.TextContent ?? string.Empty,
            relevantFaqs,
            history);
    }

    private static double ScoreFaq(FaqItem faq, string? inboundText)
    {
        var normalizedMessage = TextNormalizer.Normalize(inboundText);
        if (normalizedMessage.Length == 0) return 0;

        var normalizedQuestion = TextNormalizer.Normalize(faq.Question);
        if (normalizedQuestion.Length > 0 && normalizedMessage == normalizedQuestion) return 1.0;
        if (normalizedQuestion.Length > 0 &&
            (normalizedMessage.Contains(normalizedQuestion) || normalizedQuestion.Contains(normalizedMessage)))
            return 0.92;

        var keywordScore = TextNormalizer.SplitCsv(faq.Keywords)
            .Select(TextNormalizer.Normalize).Where(v => v.Length > 0)
            .Any(normalizedMessage.Contains) ? 0.9 : 0.0;

        var messageTokens = TextNormalizer.Tokenize(normalizedMessage);
        var faqTokens = TextNormalizer.Tokenize($"{faq.Question} {faq.Keywords}");
        var overlapScore = faqTokens.Count == 0 ? 0.0
            : (double)faqTokens.Count(messageTokens.Contains) / faqTokens.Count;

        return Math.Max(keywordScore, overlapScore);
    }
}
```

---

## Step 5 — AiSafetyChecker

### `src/Pasukhi.Infrastructure/Services/AiSafetyChecker.cs`

```csharp
using System.Text.RegularExpressions;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Services;

public class AiSafetyChecker : IAiSafetyChecker
{
    private static readonly Regex UrlRegex = new(@"(https?://\S+|www\.\S+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"(?<!\d)(?:\+?\d[\d\s().-]{6,}\d)(?!\d)", RegexOptions.Compiled);
    private static readonly string[] UncertaintyPhrases =
    {
        "i think", "i'm not sure", "i am not sure", "i might be wrong", "not certain", "maybe"
    };

    public Task<AiSafetyResult> ValidateAsync(AiContext context, AiReplyResult result, CancellationToken ct = default)
    {
        if (!result.Success)
            return Task.FromResult(new AiSafetyResult(false, result.Error ?? "AI reply generation failed."));

        var text = result.ReplyText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(new AiSafetyResult(false, "AI reply was empty."));

        if (text.Length > 1000)
            return Task.FromResult(new AiSafetyResult(false, "AI reply was too long."));

        var lowered = text.ToLowerInvariant();
        if (UncertaintyPhrases.Any(lowered.Contains))
            return Task.FromResult(new AiSafetyResult(false, "AI reply contained uncertainty language."));

        var allowedContext = BuildAllowedContext(context);
        var unsupportedContact = ExtractContactValues(text)
            .FirstOrDefault(value => !allowedContext.Contains(value, StringComparison.OrdinalIgnoreCase));
        if (unsupportedContact is not null)
            return Task.FromResult(new AiSafetyResult(false, $"AI reply included unsupported contact detail: {unsupportedContact}"));

        return Task.FromResult(new AiSafetyResult(true, null));
    }

    private static string BuildAllowedContext(AiContext context)
    {
        var faqText = string.Join("\n", context.RelevantFaqs.Select(f => $"{f.Question}\n{f.Answer}\n{f.Keywords}"));
        var historyText = string.Join("\n", context.ConversationHistory.Select(m => m.Content));
        return string.Join("\n", context.BusinessName, context.BusinessDescription, context.SystemPrompt,
            context.ToneDescription, context.EscalationMessage, faqText, historyText);
    }

    private static IEnumerable<string> ExtractContactValues(string text)
    {
        foreach (Match match in UrlRegex.Matches(text)) yield return match.Value.TrimEnd('.', ',', ')');
        foreach (Match match in EmailRegex.Matches(text)) yield return match.Value;
        foreach (Match match in PhoneRegex.Matches(text)) yield return match.Value.Trim();
    }
}
```

---

## Step 6 — GeminiService

### `src/Pasukhi.Infrastructure/Services/GeminiService.cs`

```csharp
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pasukhi.Application.AI;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Services;

public class GeminiService : IAiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(HttpClient httpClient, IOptions<AiOptions> options, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress ??= new Uri("https://generativelanguage.googleapis.com/v1beta/");
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds));
    }

    public async Task<AiReplyResult> GenerateReplyAsync(AiContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return Failure("Gemini API key is not configured.");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var model = _options.GetDefaultModel();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"models/{model}:generateContent");
            request.Headers.Add("x-goog-api-key", _options.ApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(BuildRequest(context), JsonOptions), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
                return Failure($"Gemini returned HTTP {(int)response.StatusCode}.", stopwatch.Elapsed);

            return ParseResponse(content, stopwatch.Elapsed);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Gemini request timed out.");
            return Failure("Gemini request timed out.", stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Gemini request failed.");
            return Failure("Gemini request failed.", stopwatch.Elapsed);
        }
    }

    private object BuildRequest(AiContext context) => new
    {
        contents = new[] { new { parts = new[] { new { text = BuildFullPrompt(context) } } } },
        generationConfig = new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "OBJECT",
                properties = new
                {
                    replyText = new { type = "STRING", nullable = true },
                    confidenceScore = new { type = "NUMBER" },
                    shouldEscalate = new { type = "BOOLEAN" },
                    escalationReason = new { type = "STRING", nullable = true }
                },
                required = new[] { "replyText", "confidenceScore", "shouldEscalate", "escalationReason" }
            },
            temperature = _options.Temperature,
            maxOutputTokens = _options.MaxTokens
        }
    };

    private static string BuildFullPrompt(AiContext context)
    {
        var systemPrompt = $"""
            You are a customer service assistant for {context.BusinessName}.
            Business description: {context.BusinessDescription ?? "Not provided."}
            Tone: {context.ToneDescription}.
            You ONLY answer using the business and FAQ context provided by the system.
            NEVER make up prices, policies, phone numbers, emails, URLs, or addresses.
            If the answer is not clearly covered by context, set shouldEscalate to true.
            Keep customer-facing replies concise and under 200 words.
            Respond in the same language the customer is using.

            {context.SystemPrompt}
            """;

        var faqs = context.RelevantFaqs.Count == 0 ? "No FAQ context is available."
            : string.Join("\n\n", context.RelevantFaqs.Select(f => $"Q: {f.Question}\nA: {f.Answer}"));

        var history = context.ConversationHistory.Count == 0 ? "No previous messages."
            : string.Join("\n", context.ConversationHistory.Select(m => $"{m.Role}: {m.Content}"));

        return $"""
            {systemPrompt}

            ---

            Customer display name: {context.CustomerDisplayName}
            Channel: {context.ChannelType}

            FAQ context:
            {faqs}

            Previous messages:
            {history}

            Latest customer message:
            {context.InboundMessageText}

            Escalation fallback message:
            {context.EscalationMessage}

            ---

            Respond with valid JSON matching the schema.
            """;
    }

    private static AiReplyResult ParseResponse(string content, TimeSpan elapsed)
    {
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return Failure("Gemini response did not include any candidates.", elapsed);

        var firstCandidate = candidates[0];
        if (!firstCandidate.TryGetProperty("content", out var contentElement) ||
            !contentElement.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
            return Failure("Gemini response did not include content parts.", elapsed);

        var part = parts[0];
        if (!part.TryGetProperty("text", out var outputText) || string.IsNullOrWhiteSpace(outputText.GetString()))
            return Failure("Gemini response did not include output text.", elapsed);

        using var outputDoc = JsonDocument.Parse(outputText.GetString()!);
        var replyRoot = outputDoc.RootElement;

        var replyText = replyRoot.TryGetProperty("replyText", out var replyEl) && replyEl.ValueKind != JsonValueKind.Null
            ? replyEl.GetString() : null;
        var confidence = replyRoot.TryGetProperty("confidenceScore", out var confEl) && confEl.TryGetDouble(out var parsed)
            ? parsed : 0;
        var shouldEscalate = replyRoot.TryGetProperty("shouldEscalate", out var escEl) && escEl.ValueKind == JsonValueKind.True;
        var escalationReason = replyRoot.TryGetProperty("escalationReason", out var reasonEl) && reasonEl.ValueKind != JsonValueKind.Null
            ? reasonEl.GetString() : null;

        var tokensUsed = root.TryGetProperty("usageMetadata", out var usage) &&
                         usage.TryGetProperty("candidatesTokenCount", out var tokens) &&
                         tokens.TryGetInt32(out var tokenCount) ? tokenCount : 0;

        return new AiReplyResult(true, replyText, Math.Clamp(confidence, 0, 1), shouldEscalate, escalationReason, tokensUsed, elapsed);
    }

    private static AiReplyResult Failure(string error, TimeSpan? elapsed = null) =>
        new(false, null, 0, true, error, 0, elapsed ?? TimeSpan.Zero, error);
}
```

---

## Step 7 — AI DTOs + BusinessPromptService + AiController

### `src/Pasukhi.Application/DTOs/Ai/BusinessPromptDtos.cs`

```csharp
namespace Pasukhi.Application.DTOs.Ai;

public record BusinessPromptDto(
    Guid Id,
    bool IsAiEnabled,
    string SystemPrompt,
    string ToneDescription,
    string EscalationMessage,
    int MaxAiTokensPerDay,
    double AiConfidenceThreshold,
    double FaqConfidenceThreshold);

public record UpsertBusinessPromptRequest(
    bool IsAiEnabled,
    string SystemPrompt,
    string ToneDescription,
    string EscalationMessage,
    int MaxAiTokensPerDay,
    double AiConfidenceThreshold,
    double FaqConfidenceThreshold);
```

### `src/Pasukhi.Application/Interfaces/IBusinessPromptService.cs`

```csharp
using Pasukhi.Application.DTOs.Ai;

namespace Pasukhi.Application.Interfaces;

public interface IBusinessPromptService
{
    Task<BusinessPromptDto?> GetAsync(CancellationToken ct = default);
    Task<BusinessPromptDto> UpsertAsync(UpsertBusinessPromptRequest request, CancellationToken ct = default);
}
```

### `src/Pasukhi.Infrastructure/Services/BusinessPromptService.cs`

```csharp
using Mapster;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Ai;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class BusinessPromptService : IBusinessPromptService
{
    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public BusinessPromptService(PasukhiDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task<BusinessPromptDto?> GetAsync(CancellationToken ct = default) =>
        await _db.BusinessPrompts.ProjectToType<BusinessPromptDto>().FirstOrDefaultAsync(ct);

    public async Task<BusinessPromptDto> UpsertAsync(UpsertBusinessPromptRequest request, CancellationToken ct = default)
    {
        var businessId = _tenantProvider.BusinessId == Guid.Empty
            ? throw new InvalidOperationException("Tenant context is required.")
            : _tenantProvider.BusinessId;

        var prompt = await _db.BusinessPrompts.FirstOrDefaultAsync(ct);

        if (prompt is null)
        {
            prompt = new BusinessPrompt { Id = Guid.NewGuid(), BusinessId = businessId };
            _db.BusinessPrompts.Add(prompt);
        }

        prompt.IsAiEnabled = request.IsAiEnabled;
        prompt.SystemPrompt = request.SystemPrompt.Trim();
        prompt.ToneDescription = request.ToneDescription.Trim();
        prompt.EscalationMessage = request.EscalationMessage.Trim();
        prompt.MaxAiTokensPerDay = Math.Max(0, request.MaxAiTokensPerDay);
        prompt.AiConfidenceThreshold = Math.Clamp(request.AiConfidenceThreshold, 0, 1);
        prompt.FaqConfidenceThreshold = Math.Clamp(request.FaqConfidenceThreshold, 0, 1);

        await _db.SaveChangesAsync(ct);
        return prompt.Adapt<BusinessPromptDto>();
    }
}
```

### `src/Pasukhi.API/Controllers/AiController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Ai;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IBusinessPromptService _prompts;

    public AiController(IBusinessPromptService prompts)
    {
        _prompts = prompts;
    }

    [HttpGet("prompt")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await _prompts.GetAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("prompt")]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertBusinessPromptRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _prompts.UpsertAsync(request, cancellationToken));
}
```

---

## Step 8 — Update InboundMessageConsumer to Wire TryApplyAiAsync

Add `IAiPromptBuilder`, `IAiService`, `IAiSafetyChecker`, and `IOptions<AiOptions>` to the constructor. Add `TryApplyAiAsync` and change the final escalation call so AI runs before giving up:

```csharp
// In Consume(), replace the final "await CreateEscalationAsync(NoMatch)" with:
await TryApplyAiAsync(e, conversation, message, channelType.Value, ct);
```

Add the method:

```csharp
private async Task<bool> TryApplyAiAsync(
    InboundMessageEvent inboundEvent,
    Conversation conversation,
    Message inboundMessage,
    ChannelType channelType,
    CancellationToken ct)
{
    var aiContext = await _aiPromptBuilder.BuildAsync(conversation, inboundMessage, channelType, ct);
    if (aiContext is null)
        return await CreateEscalationAsync(conversation, inboundMessage, channelType, EscalationReason.NoMatch, "No AI prompt configured.", null, ct);

    if (!aiContext.IsAiEnabled)
        return await CreateEscalationAsync(conversation, inboundMessage, channelType, EscalationReason.NoMatch, "AI fallback is disabled.", null, ct);

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var metric = await GetOrCreateMetricAsync(inboundMessage.BusinessId, channelType, today, ct);
    if (metric.AiTokensUsed + _aiOptions.MaxTokens > aiContext.MaxAiTokensPerDay)
        return await CreateEscalationAsync(conversation, inboundMessage, channelType, EscalationReason.NoMatch, "AI daily token budget exceeded.", null, ct);

    var result = await _aiService.GenerateReplyAsync(aiContext, ct);
    if (!result.Success)
        return await CreateEscalationAsync(conversation, inboundMessage, channelType, EscalationReason.NoMatch, result.Error ?? "AI fallback failed.", result.ReplyText, ct);

    if (result.ConfidenceScore < aiContext.AiConfidenceThreshold)
        return await CreateEscalationAsync(conversation, inboundMessage, channelType, EscalationReason.LowAiConfidence,
            $"AI confidence {result.ConfidenceScore:0.00} below threshold {aiContext.AiConfidenceThreshold:0.00}.", result.ReplyText, ct);

    if (result.ShouldEscalate)
        return await CreateEscalationAsync(conversation, inboundMessage, channelType, EscalationReason.NoMatch, result.EscalationReason ?? "AI requested escalation.", result.ReplyText, ct);

    var safety = await _aiSafetyChecker.ValidateAsync(aiContext, result, ct);
    if (!safety.Passed)
        return await CreateEscalationAsync(conversation, inboundMessage, channelType, EscalationReason.SafetyCheckFailed, safety.RejectionReason ?? "AI reply failed safety checks.", result.ReplyText, ct);

    return await TryCreateOutboundAutoReplyAsync(
        inboundEvent, conversation, inboundMessage, channelType,
        MessageSource.AiAutoReply, result.ReplyText!,
        matchedFaqItemId: null, matchedRuleId: null,
        aiConfidenceScore: result.ConfidenceScore, aiTokensUsed: result.TokensUsed, ct);
}
```

---

## Step 9 — Migration

```bash
dotnet ef migrations add AddAiTokensUsedToDailyMetric --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
dotnet ef database update --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
```

---

## Step 10 — Register Services in Program.cs

```csharp
// AI options
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("AI"));

// AI services — provider selection
var aiProvider = builder.Configuration["AI:Provider"] ?? "Gemini";
if (aiProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IAiService, OpenAiService>();
}
else
{
    builder.Services.AddHttpClient<IAiService, GeminiService>();
}

builder.Services.AddScoped<IAiPromptBuilder, AiPromptBuilder>();
builder.Services.AddScoped<IAiSafetyChecker, AiSafetyChecker>();
builder.Services.AddScoped<IBusinessPromptService, BusinessPromptService>();
```

Add to `appsettings.json`:

```json
"AI": {
  "Provider": "Gemini",
  "ApiKey": "",
  "Model": "gemini-2.0-flash-lite",
  "MaxTokens": 500,
  "Temperature": 0.3,
  "RequestTimeoutSeconds": 30,
  "ReasoningEffort": "low"
}
```

Add to `appsettings.Development.json`:

```json
"AI": {
  "Provider": "Gemini",
  "ApiKey": "YOUR_GEMINI_API_KEY"
}
```

---

## Verification

```bash
dotnet build
```

1. Configure a `BusinessPrompt` via `PUT /api/ai/prompt` with `IsAiEnabled: true`.
2. Send an inbound message that doesn't match any FAQ or rule.
3. Confirm in logs:
   ```
   AiAutoReply auto-reply queued. InboundMessage=... OutboundMessage=...
   ```
4. Send a message with no clear answer in context. Confirm:
   ```
   Conversation escalated. Reason=LowAiConfidence ...
   ```

---

## Commit

```bash
git add src/ docs/codex/phase-8.md
git commit -m "feat(08): AI fallback pipeline — Gemini + OpenAI + prompt builder + safety checker"
```

---

## What's Next

Phase 9: `docs/codex/phase-9.md` — Settings service: `BusinessSetting` entity, real `SettingsService`, `SettingsController`, and working hours enforcement.
