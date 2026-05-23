using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Text;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Pasukhi.Infrastructure.Services;

public class FaqMatcher : BaseMatcher, IFaqMatcher
{
    private const double DefaultThreshold = 0.85;

    public FaqMatcher(PasukhiDbContext db) : base(db) { }

    public async Task<FaqMatchResult?> FindBestMatchAsync(
        Guid businessId,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        if (businessId == Guid.Empty || string.IsNullOrWhiteSpace(messageText))
            return null;

        var threshold = await TenantQuery(Db.BusinessPrompts, businessId)
            .Select(p => (double?)p.FaqConfidenceThreshold)
            .FirstOrDefaultAsync(cancellationToken) ?? DefaultThreshold;

        var faqs = await TenantQuery(Db.FaqItems, businessId)
            .Where(f => f.IsActive)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Question)
            .ToListAsync(cancellationToken);

        var normalizedMessage = TextNormalizer.Normalize(messageText);
        var best = faqs
            .Select(faq => new { Faq = faq, Confidence = ScoreFaq(faq, normalizedMessage) })
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.Faq.SortOrder)
            .FirstOrDefault();

        if (best == null || best.Confidence < threshold)
            return null;

        best.Faq.MatchCount++;
        return new FaqMatchResult(best.Faq, best.Confidence);
    }

    private static double ScoreFaq(FaqItem faq, string normalizedMessage)
    {
        var normalizedQuestion = TextNormalizer.Normalize(faq.Question);

        if (normalizedMessage == normalizedQuestion)
            return 1.0;

        if (normalizedMessage.Contains(normalizedQuestion) || normalizedQuestion.Contains(normalizedMessage))
            return 0.92;

        return Math.Max(
            ScoreKeywords(faq.Keywords, normalizedMessage),
            ScoreTokenOverlap(normalizedMessage, normalizedQuestion));
    }

    private static double ScoreKeywords(string? keywords, string normalizedMessage)
    {
        var parts = TextNormalizer.SplitCsv(keywords)
            .Select(TextNormalizer.Normalize)
            .Where(value => value.Length > 0)
            .ToList();

        if (parts.Count == 0)
            return 0.0;

        var matches = parts.Count(normalizedMessage.Contains);
        if (matches == 0)
            return 0.0;

        var ratio = (double)matches / parts.Count;
        return Math.Min(0.9, 0.85 + (ratio * 0.05));
    }

    private static double ScoreTokenOverlap(string normalizedMessage, string normalizedQuestion)
    {
        var messageTokens = TextNormalizer.Tokenize(normalizedMessage);
        var questionTokens = TextNormalizer.Tokenize(normalizedQuestion);

        if (messageTokens.Count == 0 || questionTokens.Count == 0)
            return 0.0;

        var overlap = questionTokens.Count(messageTokens.Contains);
        var ratio = (double)overlap / questionTokens.Count;
        return ratio == 0 ? 0.0 : Math.Min(0.85, 0.4 + (ratio * 0.45));
    }
}
