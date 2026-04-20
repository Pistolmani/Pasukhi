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
        "i think",
        "i'm not sure",
        "i am not sure",
        "i might be wrong",
        "not certain",
        "maybe"
    };

    public Task<AiSafetyResult> ValidateAsync(
        AiContext context,
        AiReplyResult result,
        CancellationToken ct = default)
    {
        if (!result.Success)
        {
            return Task.FromResult(new AiSafetyResult(false, result.Error ?? "AI reply generation failed."));
        }

        var text = result.ReplyText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new AiSafetyResult(false, "AI reply was empty."));
        }

        if (text.Length > 1000)
        {
            return Task.FromResult(new AiSafetyResult(false, "AI reply was too long."));
        }

        var lowered = text.ToLowerInvariant();
        if (UncertaintyPhrases.Any(lowered.Contains))
        {
            return Task.FromResult(new AiSafetyResult(false, "AI reply contained uncertainty language."));
        }

        var allowedContext = BuildAllowedContext(context);
        var unsupportedContact = ExtractContactValues(text)
            .FirstOrDefault(value => !allowedContext.Contains(value, StringComparison.OrdinalIgnoreCase));
        if (unsupportedContact is not null)
        {
            return Task.FromResult(new AiSafetyResult(false, $"AI reply included unsupported contact detail: {unsupportedContact}"));
        }

        return Task.FromResult(new AiSafetyResult(true, null));
    }

    private static string BuildAllowedContext(AiContext context)
    {
        var faqText = string.Join(
            "\n",
            context.RelevantFaqs.Select(f => $"{f.Question}\n{f.Answer}\n{f.Keywords}"));
        var historyText = string.Join("\n", context.ConversationHistory.Select(m => m.Content));

        return string.Join(
            "\n",
            context.BusinessName,
            context.BusinessDescription,
            context.SystemPrompt,
            context.ToneDescription,
            context.EscalationMessage,
            faqText,
            historyText);
    }

    private static IEnumerable<string> ExtractContactValues(string text)
    {
        foreach (Match match in UrlRegex.Matches(text))
        {
            yield return match.Value.TrimEnd('.', ',', ')');
        }

        foreach (Match match in EmailRegex.Matches(text))
        {
            yield return match.Value;
        }

        foreach (Match match in PhoneRegex.Matches(text))
        {
            yield return match.Value.Trim();
        }
    }
}
