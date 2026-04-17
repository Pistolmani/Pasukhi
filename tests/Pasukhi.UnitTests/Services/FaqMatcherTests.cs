using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Services;

namespace Pasukhi.UnitTests.Services;

public class FaqMatcherTests
{
    [Fact]
    public async Task Exact_match_returns_full_confidence_and_increments_count()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var faq = Faq(businessId, "რა ღირს?", "It costs 10 GEL.");
        db.FaqItems.Add(faq);
        await db.SaveChangesAsync();

        var result = await new FaqMatcher(db).FindBestMatchAsync(businessId, "რა ღირს?");

        Assert.NotNull(result);
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal(faq.Id, result.FaqItem.Id);
        Assert.Equal(1, faq.MatchCount);
    }

    [Fact]
    public async Task Substring_match_returns_expected_confidence()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var faq = Faq(businessId, "shipping", "We ship tomorrow.");
        db.FaqItems.Add(faq);
        await db.SaveChangesAsync();

        var result = await new FaqMatcher(db).FindBestMatchAsync(businessId, "tell me about shipping");

        Assert.NotNull(result);
        Assert.Equal(0.92, result.Confidence);
    }

    [Fact]
    public async Task Georgian_keyword_hit_scores_above_default_threshold()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var faq = Faq(businessId, "რა ღირს?", "ფასი არის 10 ლარი.", "price, cost, ფასი");
        db.FaqItems.Add(faq);
        await db.SaveChangesAsync();

        var result = await new FaqMatcher(db).FindBestMatchAsync(businessId, "რა ფასია?");

        Assert.NotNull(result);
        Assert.True(result.Confidence >= 0.85);
        Assert.Equal(faq.Id, result.FaqItem.Id);
    }

    [Fact]
    public async Task Below_threshold_returns_null()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        db.FaqItems.Add(Faq(businessId, "shipping", "We ship tomorrow.", "delivery"));
        await db.SaveChangesAsync();

        var result = await new FaqMatcher(db).FindBestMatchAsync(businessId, "hello there");

        Assert.Null(result);
    }

    [Fact]
    public async Task Inactive_faq_is_excluded()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var faq = Faq(businessId, "რა ღირს?", "10 GEL");
        faq.IsActive = false;
        db.FaqItems.Add(faq);
        await db.SaveChangesAsync();

        var result = await new FaqMatcher(db).FindBestMatchAsync(businessId, "რა ღირს?");

        Assert.Null(result);
        Assert.Equal(0, faq.MatchCount);
    }

    [Fact]
    public async Task Business_prompt_threshold_is_honored()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        db.BusinessPrompts.Add(new BusinessPrompt
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            SystemPrompt = "Be helpful.",
            FaqConfidenceThreshold = 0.95
        });
        db.FaqItems.Add(Faq(businessId, "რა ღირს?", "10 GEL", "ფასი, price, cost"));
        await db.SaveChangesAsync();

        var result = await new FaqMatcher(db).FindBestMatchAsync(businessId, "რა ფასია?");

        Assert.Null(result);
    }

    private static FaqItem Faq(Guid businessId, string question, string answer, string? keywords = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Question = question,
            Answer = answer,
            Keywords = keywords,
            IsActive = true,
            SortOrder = 0
        };
}
