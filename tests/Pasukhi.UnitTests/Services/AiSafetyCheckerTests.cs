using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Services;

namespace Pasukhi.UnitTests.Services;

public class AiSafetyCheckerTests
{
    [Fact]
    public async Task ValidateAsync_accepts_safe_reply()
    {
        var checker = new AiSafetyChecker();
        var result = await checker.ValidateAsync(NewContext(), NewReply("We are open from 10:00 to 18:00."));

        Assert.True(result.Passed);
        Assert.Null(result.RejectionReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_rejects_empty_replies(string reply)
    {
        var checker = new AiSafetyChecker();
        var result = await checker.ValidateAsync(NewContext(), NewReply(reply));

        Assert.False(result.Passed);
        Assert.Equal("AI reply was empty.", result.RejectionReason);
    }

    [Fact]
    public async Task ValidateAsync_rejects_too_long_replies()
    {
        var checker = new AiSafetyChecker();
        var result = await checker.ValidateAsync(NewContext(), NewReply(new string('x', 1001)));

        Assert.False(result.Passed);
        Assert.Equal("AI reply was too long.", result.RejectionReason);
    }

    [Theory]
    [InlineData("I think we are open tomorrow.")]
    [InlineData("Maybe this product is available.")]
    [InlineData("I am not sure about that policy.")]
    public async Task ValidateAsync_rejects_uncertainty_phrases(string reply)
    {
        var checker = new AiSafetyChecker();
        var result = await checker.ValidateAsync(NewContext(), NewReply(reply));

        Assert.False(result.Passed);
        Assert.Equal("AI reply contained uncertainty language.", result.RejectionReason);
    }

    [Theory]
    [InlineData("Visit https://made-up.example for details.")]
    [InlineData("Email support@made-up.example for help.")]
    [InlineData("Call +1 555 123 4567 for details.")]
    public async Task ValidateAsync_rejects_contact_details_not_present_in_context(string reply)
    {
        var checker = new AiSafetyChecker();
        var result = await checker.ValidateAsync(NewContext(), NewReply(reply));

        Assert.False(result.Passed);
        Assert.StartsWith("AI reply included unsupported contact detail:", result.RejectionReason);
    }

    [Fact]
    public async Task ValidateAsync_allows_contact_details_present_in_context()
    {
        var checker = new AiSafetyChecker();
        var context = NewContext();
        var result = await checker.ValidateAsync(
            context,
            NewReply("You can email info@pasukhi.test or visit https://pasukhi.test."));

        Assert.True(result.Passed);
    }

    private static AiContext NewContext() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Pasukhi Test",
        "Official site: https://pasukhi.test. Email: info@pasukhi.test.",
        "Answer only from the supplied context.",
        "professional and friendly",
        "Let me connect you with our team.",
        true,
        50_000,
        0.7,
        ChannelType.Instagram,
        "Customer",
        "What are your hours?",
        new[]
        {
            new AiFaqContextItem(Guid.NewGuid(), "Hours?", "We are open from 10:00 to 18:00.", null)
        },
        Array.Empty<AiMessage>());

    private static AiReplyResult NewReply(string? text) => new(
        true,
        text,
        0.9,
        false,
        null,
        12,
        TimeSpan.FromMilliseconds(5));
}
