using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pasukhi.Application.AI;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Services;

public class GeminiService : BaseAiService
{
    public GeminiService(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<GeminiService> logger)
        : base(httpClient, options.Value, logger)
    {
        HttpClient.BaseAddress ??= new Uri("https://generativelanguage.googleapis.com/v1beta/");
    }

    protected override string ProviderName => "Gemini";

    protected override HttpRequestMessage BuildHttpRequest(AiContext context)
    {
        var model = Options.GetDefaultModel();
        var request = new HttpRequestMessage(HttpMethod.Post, $"models/{model}:generateContent");
        request.Headers.Add("x-goog-api-key", Options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(BuildRequestBody(context), JsonOptions),
            Encoding.UTF8,
            "application/json");
        return request;
    }

    protected override AiReplyResult ParseResponse(string content, TimeSpan elapsed)
    {
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return Failure("Gemini response did not include any candidates.", elapsed);

        var firstCandidate = candidates[0];
        if (!firstCandidate.TryGetProperty("content", out var contentEl) ||
            !contentEl.TryGetProperty("parts", out var parts) ||
            parts.GetArrayLength() == 0)
            return Failure("Gemini response did not include content parts.", elapsed);

        var part = parts[0];
        if (!part.TryGetProperty("text", out var outputText) || string.IsNullOrWhiteSpace(outputText.GetString()))
            return Failure("Gemini response did not include output text.", elapsed);

        var tokensUsed = TryReadTotalTokens(root);
        using var outputDoc = JsonDocument.Parse(outputText.GetString()!);
        return ParseOutputJson(outputDoc.RootElement, tokensUsed, elapsed);
    }

    private object BuildRequestBody(AiContext context) => new
    {
        contents = new[]
        {
            new
            {
                parts = new[]
                {
                    new { text = BuildFullPrompt(context) }
                }
            }
        },
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
            temperature = Options.Temperature,
            maxOutputTokens = Options.MaxTokens
        }
    };

    private string BuildFullPrompt(AiContext context)
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

        return $"""
            {systemPrompt}

            ---

            Customer display name: {context.CustomerDisplayName}
            Channel: {context.ChannelType}

            FAQ context:
            {FormatFaqs(context)}

            Previous messages:
            {FormatHistory(context)}

            Latest customer message:
            {context.InboundMessageText}

            Escalation fallback message:
            {context.EscalationMessage}

            ---

            Respond with valid JSON matching the schema.
            """;
    }

    private static int TryReadTotalTokens(JsonElement root)
    {
        if (root.TryGetProperty("usageMetadata", out var usage) &&
            usage.TryGetProperty("candidatesTokenCount", out var tokens) &&
            tokens.TryGetInt32(out var tokenCount))
        {
            return tokenCount;
        }

        return 0;
    }
}
