using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pasukhi.Application.BotReadiness;
using Pasukhi.Application.DTOs.Ai;
using Pasukhi.Application.DTOs.BotReadiness;
using Pasukhi.Application.DTOs.Faqs;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class BotReadinessService : IBotReadinessService
{
    private const string PromptBlockStart = "[PASUKHI BOT READINESS CONTEXT START]";
    private const string PromptBlockEnd = "[PASUKHI BOT READINESS CONTEXT END]";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly BotReadinessOptions _options;
    private readonly IFaqService _faqService;
    private readonly IBusinessPromptService _promptService;

    public BotReadinessService(
        PasukhiDbContext db,
        ITenantProvider tenantProvider,
        IOptions<BotReadinessOptions> options,
        IFaqService faqService,
        IBusinessPromptService promptService)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _options = options.Value;
        _faqService = faqService;
        _promptService = promptService;
    }

    public Task<BotReadinessTemplateDto> GetTemplateAsync(CancellationToken ct = default)
    {
        var sections = _options.Sections.Select(s => new BotReadinessSectionDto(
            s.Key, s.Label, s.Description,
            s.Questions.Select(q => new BotReadinessQuestionDto(
                q.Key, q.Label, q.HelpText, q.InputType, q.Required, q.Weight
            )).ToList()
        )).ToList();

        return Task.FromResult(new BotReadinessTemplateDto(sections));
    }

    public async Task<BotReadinessReportDto> GetReportAsync(CancellationToken ct = default)
    {
        var answers = await _db.BotQuestionnaireAnswers
            .OrderBy(a => a.QuestionKey)
            .ToListAsync(ct);

        var answersByKey = answers.ToDictionary(a => a.QuestionKey);

        var suggestions = await _db.BotKnowledgeSuggestions
            .OrderBy(s => s.Status)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var totalWeight = 0;
        var answeredWeight = 0;
        var gaps = new List<BotReadinessGapDto>();
        var sectionCompletion = new List<BotReadinessSectionCompletionDto>();

        foreach (var section in _options.Sections)
        {
            var sectionTotal = 0;
            var sectionAnswered = 0;

            foreach (var question in section.Questions)
            {
                var weight = question.Weight;
                totalWeight += weight;
                sectionTotal += weight;

                answersByKey.TryGetValue(question.Key, out var answer);
                if (IsAnswered(answer))
                {
                    answeredWeight += weight;
                    sectionAnswered += weight;
                }
                else if (question.Required)
                {
                    gaps.Add(new BotReadinessGapDto(
                        question.Key, question.Label, section.Key, section.Label, weight));
                }
            }

            var score = sectionTotal > 0
                ? (int)Math.Round((double)sectionAnswered / sectionTotal * 100)
                : 0;

            sectionCompletion.Add(new BotReadinessSectionCompletionDto(
                section.Key, section.Label, sectionAnswered, sectionTotal, score));
        }

        var readinessScore = totalWeight > 0
            ? (int)Math.Round((double)answeredWeight / totalWeight * 100)
            : 0;

        return new BotReadinessReportDto(
            readinessScore,
            answeredWeight,
            totalWeight,
            answers.Select(MapAnswer).ToList(),
            gaps,
            sectionCompletion,
            suggestions.Select(MapSuggestion).ToList());
    }

    public async Task<BotReadinessReportDto> SaveAnswersAsync(
        SaveBotAnswersRequest request, CancellationToken ct = default)
    {
        _ = EnsureTenant();
        var knownKeys = FlatQuestionKeys();

        var existing = await _db.BotQuestionnaireAnswers.ToListAsync(ct);
        var existingByKey = existing.ToDictionary(a => a.QuestionKey);

        foreach (var item in request.Answers)
        {
            if (!knownKeys.Contains(item.QuestionKey))
                continue;

            var answerText = item.IsSkipped ? null : item.AnswerText?.Trim();

            if (existingByKey.TryGetValue(item.QuestionKey, out var answer))
            {
                answer.AnswerText = answerText;
                answer.IsSkipped = item.IsSkipped;
            }
            else
            {
                _db.BotQuestionnaireAnswers.Add(new BotQuestionnaireAnswer
                {
                    Id = Guid.NewGuid(),
                    QuestionKey = item.QuestionKey,
                    AnswerText = answerText,
                    IsSkipped = item.IsSkipped
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetReportAsync(ct);
    }

    public async Task<BotReadinessReportDto> GenerateSuggestionsAsync(CancellationToken ct = default)
    {
        var businessId = EnsureTenant();

        var answers = await _db.BotQuestionnaireAnswers
            .ToListAsync(ct);
        var answersByKey = answers.ToDictionary(a => a.QuestionKey);

        // FAQ suggestions
        foreach (var def in FaqSuggestionDefinitions())
        {
            answersByKey.TryGetValue(def.QuestionKey, out var answer);
            if (!IsAnswered(answer))
                continue;

            await CreateSuggestionIfMissing(businessId, SuggestionType.Faq,
                [def.QuestionKey],
                new FaqPayload(def.Question, answer!.AnswerText!.Trim(), def.Keywords, true, def.SortOrder),
                ct);
        }

        // Prompt context suggestion
        var context = BuildPromptContext(answersByKey);
        if (context is not null)
        {
            var sourceKeys = context.Values.Keys.Order().ToList();
            await CreateSuggestionIfMissing(businessId, SuggestionType.PromptContext,
                sourceKeys,
                new PromptContextPayload(context.Text, context.Values),
                ct);
        }

        await _db.SaveChangesAsync(ct);
        return await GetReportAsync(ct);
    }

    public async Task<BotKnowledgeSuggestionDto> ApproveSuggestionAsync(Guid id, CancellationToken ct = default)
    {
        var suggestion = await _db.BotKnowledgeSuggestions.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Suggestion {id} not found.");

        if (suggestion.Status == SuggestionStatus.Approved)
            return MapSuggestion(suggestion);

        if (suggestion.Status == SuggestionStatus.Rejected)
            throw new InvalidOperationException("Rejected suggestions cannot be approved.");

        switch (suggestion.Type)
        {
            case SuggestionType.Faq:
                await ApproveFaq(suggestion, ct);
                break;
            case SuggestionType.PromptContext:
                await ApprovePromptContext(suggestion, ct);
                break;
            default:
                throw new InvalidOperationException($"Unsupported suggestion type: {suggestion.Type}");
        }

        suggestion.Status = SuggestionStatus.Approved;
        suggestion.ApprovedAt = DateTime.UtcNow;
        suggestion.RejectedAt = null;
        await _db.SaveChangesAsync(ct);

        return MapSuggestion(suggestion);
    }

    public async Task<BotKnowledgeSuggestionDto> RejectSuggestionAsync(Guid id, CancellationToken ct = default)
    {
        var suggestion = await _db.BotKnowledgeSuggestions.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Suggestion {id} not found.");

        if (suggestion.Status == SuggestionStatus.Approved)
            throw new InvalidOperationException("Approved suggestions cannot be rejected.");

        suggestion.Status = SuggestionStatus.Rejected;
        suggestion.RejectedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return MapSuggestion(suggestion);
    }

    // --- Private helpers ---

    private async Task ApproveFaq(BotKnowledgeSuggestion suggestion, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<FaqPayload>(suggestion.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Invalid FAQ payload.");

        await _faqService.CreateAsync(new CreateFaqItemRequest(
            payload.Question.Trim(),
            payload.Answer.Trim(),
            string.IsNullOrWhiteSpace(payload.Keywords) ? null : payload.Keywords.Trim(),
            payload.IsActive,
            payload.SortOrder
        ), ct);
    }

    private async Task ApprovePromptContext(BotKnowledgeSuggestion suggestion, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<PromptContextPayload>(suggestion.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Invalid prompt context payload.");

        var contextBlock = ManagedPromptBlock(payload.Context);

        var existing = await _promptService.GetAsync(ct);
        if (existing is not null)
        {
            var updatedPrompt = ReplaceManagedPromptBlock(existing.SystemPrompt, contextBlock);
            await _promptService.UpsertAsync(new UpsertBusinessPromptRequest(
                existing.IsAiEnabled,
                updatedPrompt,
                existing.ToneDescription,
                existing.EscalationMessage,
                existing.MaxAiTokensPerDay,
                existing.AiConfidenceThreshold,
                existing.FaqConfidenceThreshold
            ), ct);
        }
        else
        {
            var tone = payload.Values.GetValueOrDefault("tone_preference", "professional and friendly");
            await _promptService.UpsertAsync(new UpsertBusinessPromptRequest(
                false,
                contextBlock,
                tone,
                "Let me connect you with our team.",
                50000,
                0.7,
                0.85
            ), ct);
        }
    }

    private async Task CreateSuggestionIfMissing(
        Guid businessId, SuggestionType type, List<string> sourceKeys, object payload, CancellationToken ct)
    {
        var sortedKeys = sourceKeys.Order().ToList();
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

        var hashInput = JsonSerializer.Serialize(new
        {
            type = type.ToString(),
            sourceQuestionKeys = sortedKeys,
            payload
        }, JsonOptions);

        var hash = ComputeSha256(hashInput);

        var exists = await _db.BotKnowledgeSuggestions
            .AnyAsync(s => s.BusinessId == businessId && s.Type == type && s.DedupeHash == hash, ct);

        if (exists) return;

        _db.BotKnowledgeSuggestions.Add(new BotKnowledgeSuggestion
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Type = type,
            Status = SuggestionStatus.Pending,
            SourceQuestionKeys = sortedKeys,
            PayloadJson = payloadJson,
            DedupeHash = hash
        });
    }

    private PromptContextResult? BuildPromptContext(Dictionary<string, BotQuestionnaireAnswer> answersByKey)
    {
        var labels = _options.Sections
            .SelectMany(s => s.Questions)
            .ToDictionary(q => q.Key, q => q.Label);

        var values = new Dictionary<string, string>();
        foreach (var (key, answer) in answersByKey)
        {
            if (IsAnswered(answer))
                values[key] = answer.AnswerText!.Trim();
        }

        if (values.Count == 0)
            return null;

        var lines = values.Select(kv =>
            $"- {(labels.GetValueOrDefault(kv.Key) ?? kv.Key)}: {kv.Value}");

        var text = "Approved business context from Bot Readiness:\n" + string.Join("\n", lines);
        return new PromptContextResult(text, values);
    }

    private static string ManagedPromptBlock(string context) =>
        $"{PromptBlockStart}\n{context.Trim()}\n{PromptBlockEnd}";

    private static string ReplaceManagedPromptBlock(string current, string block)
    {
        var startIdx = current.IndexOf(PromptBlockStart, StringComparison.Ordinal);
        var endIdx = current.IndexOf(PromptBlockEnd, StringComparison.Ordinal);

        if (startIdx >= 0 && endIdx >= 0)
        {
            var before = current[..startIdx];
            var after = current[(endIdx + PromptBlockEnd.Length)..];
            return (before + block + after).Trim();
        }

        return string.IsNullOrWhiteSpace(current)
            ? block
            : $"{current.Trim()}\n\n{block}";
    }

    private static bool IsAnswered(BotQuestionnaireAnswer? answer) =>
        answer is not null && !answer.IsSkipped && !string.IsNullOrWhiteSpace(answer.AnswerText);

    private HashSet<string> FlatQuestionKeys() =>
        _options.Sections
            .SelectMany(s => s.Questions)
            .Select(q => q.Key)
            .ToHashSet();

    private static BotReadinessAnswerDto MapAnswer(BotQuestionnaireAnswer a) =>
        new(a.Id, a.BusinessId, a.QuestionKey, a.AnswerText, a.IsSkipped, a.UpdatedAt);

    private static BotKnowledgeSuggestionDto MapSuggestion(BotKnowledgeSuggestion s)
    {
        object? payload = null;
        try { payload = JsonSerializer.Deserialize<object>(s.PayloadJson, JsonOptions); }
        catch { /* leave null if unparseable */ }

        return new BotKnowledgeSuggestionDto(
            s.Id, s.BusinessId, s.Type, s.Status,
            s.SourceQuestionKeys, payload,
            s.CreatedAt, s.ApprovedAt, s.RejectedAt);
    }

    private static string ComputeSha256(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    private Guid EnsureTenant()
    {
        if (_tenantProvider.BusinessId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required.");
        return _tenantProvider.BusinessId;
    }

    // --- FAQ suggestion definitions (mirrors Laravel faqSuggestionDefinitions) ---

    private static List<FaqSuggestionDef> FaqSuggestionDefinitions() =>
    [
        new("top_products", "What products or services do you offer?",
            "products, services, items, პროდუქტი, მომსახურება", 10),
        new("price_range", "How much do your products or services cost?",
            "price, cost, pricing, ფასი, ღირებულება", 20),
        new("custom_order_policy", "Do you accept custom orders?",
            "custom, individual, order, შეკვეთა, ინდივიდუალური", 30),
        new("delivery_policy", "Do you offer delivery?",
            "delivery, shipping, courier, მიწოდება, მიტანა", 40),
        new("pickup_policy", "Can I pick up my order?",
            "pickup, collect, address, გატანა, მისამართი", 50),
        new("payment_methods", "What payment methods do you accept?",
            "payment, pay, card, cash, transfer, გადახდა", 60),
        new("return_policy", "Can I return or exchange an order?",
            "return, exchange, refund, დაბრუნება, გადაცვლა", 70),
        new("production_time", "How long does production take?",
            "production, time, ready, მზადდება, დრო", 80),
    ];

    // --- Internal types ---

    private record FaqSuggestionDef(string QuestionKey, string Question, string Keywords, int SortOrder);
    private record FaqPayload(string Question, string Answer, string? Keywords, bool IsActive, int SortOrder);
    private record PromptContextPayload(string Context, Dictionary<string, string> Values);
    private record PromptContextResult(string Text, Dictionary<string, string> Values);
}
