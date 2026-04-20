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
        if (prompt is null)
        {
            return null;
        }

        var business = await _db.Businesses
            .FirstOrDefaultAsync(b => b.Id == inboundMessage.BusinessId, ct);

        var faqs = await _db.FaqItems
            .Where(f => f.BusinessId == inboundMessage.BusinessId && f.IsActive)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Question)
            .ToListAsync(ct);

        var relevantFaqs = faqs
            .Select(faq => new
            {
                Faq = faq,
                Score = ScoreFaq(faq, inboundMessage.TextContent)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Faq.SortOrder)
            .ThenBy(x => x.Faq.Question)
            .Take(MaxFaqContextItems)
            .Select(x => new AiFaqContextItem(
                x.Faq.Id,
                x.Faq.Question,
                x.Faq.Answer,
                x.Faq.Keywords))
            .ToList();

        var history = await _db.Messages
            .Where(m =>
                m.BusinessId == inboundMessage.BusinessId &&
                m.ConversationId == conversation.Id &&
                m.Id != inboundMessage.Id &&
                m.TextContent != null &&
                m.TextContent != string.Empty)
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
        if (normalizedMessage.Length == 0)
        {
            return 0;
        }

        var normalizedQuestion = TextNormalizer.Normalize(faq.Question);
        if (normalizedQuestion.Length > 0 && normalizedMessage == normalizedQuestion)
        {
            return 1.0;
        }

        if (normalizedQuestion.Length > 0 &&
            (normalizedMessage.Contains(normalizedQuestion) || normalizedQuestion.Contains(normalizedMessage)))
        {
            return 0.92;
        }

        var keywordScore = TextNormalizer.SplitCsv(faq.Keywords)
            .Select(TextNormalizer.Normalize)
            .Where(value => value.Length > 0)
            .Any(normalizedMessage.Contains)
            ? 0.9
            : 0.0;

        var messageTokens = TextNormalizer.Tokenize(normalizedMessage);
        var faqTokens = TextNormalizer.Tokenize($"{faq.Question} {faq.Keywords}");
        var overlapScore = faqTokens.Count == 0
            ? 0.0
            : (double)faqTokens.Count(messageTokens.Contains) / faqTokens.Count;

        return Math.Max(keywordScore, overlapScore);
    }
}
