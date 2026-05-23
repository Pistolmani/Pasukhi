using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pasukhi.Application.AI;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Services;

public class OpenAiService : BaseAiService
{
    public OpenAiService(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<OpenAiService> logger)
        : base(httpClient, options.Value, logger)
    {
        HttpClient.BaseAddress ??= new Uri("https://api.openai.com/v1/");
    }

    protected override string ProviderName => "OpenAI";

    protected override HttpRequestMessage BuildHttpRequest(AiContext context)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(BuildRequestBody(context), JsonOptions),
            Encoding.UTF8,
            "application/json");
        return request;
    }

    protected override AiReplyResult ParseResponse(string content, TimeSpan elapsed)
    {
        using var doc = JsonDocument.Parse(content);
        var outputText = FindOutputText(doc.RootElement);
        if (string.IsNullOrWhiteSpace(outputText))
            return Failure("OpenAI response did not include output text.", elapsed);

        var tokensUsed = TryReadTotalTokens(doc.RootElement);
        using var outputDoc = JsonDocument.Parse(outputText);
        return ParseOutputJson(outputDoc.RootElement, tokensUsed, elapsed);
    }

    private object BuildRequestBody(AiContext context) => new
    {
        model = string.IsNullOrWhiteSpace(Options.Model) ? Options.GetDefaultModel() : Options.Model,
        input = new object[]
        {
            new
            {
                role = "system",
                content = new object[]
                {
                    new { type = "input_text", text = BuildSystemPrompt(context) }
                }
            },
            new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "input_text", text = BuildUserPrompt(context) }
                }
            }
        },
        text = new
        {
            format = new
            {
                type = "json_schema",
                name = "pasukhi_ai_reply",
                strict = true,
                schema = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        replyText = new { type = new[] { "string", "null" } },
                        confidenceScore = new { type = "number", minimum = 0, maximum = 1 },
                        shouldEscalate = new { type = "boolean" },
                        escalationReason = new { type = new[] { "string", "null" } }
                    },
                    required = new[] { "replyText", "confidenceScore", "shouldEscalate", "escalationReason" }
                }
            }
        },
        max_output_tokens = Options.MaxTokens,
        temperature = Options.Temperature,
        reasoning = new { effort = Options.ReasoningEffort }
    };

    private static string BuildSystemPrompt(AiContext context) =>
        $"""
        {context.SystemPrompt}

        You are a customer service assistant for {context.BusinessName}.
        Business description: {context.BusinessDescription ?? "Not provided."}
        Tone: {context.ToneDescription}.
        You ONLY answer using the business and FAQ context provided by the system.
        NEVER make up prices, policies, phone numbers, emails, URLs, or addresses.
        If the answer is not clearly covered by context, set shouldEscalate to true.
        Keep customer-facing replies concise and under 200 words.
        Respond in the same language the customer is using.
        """;

    private string BuildUserPrompt(AiContext context) =>
        $"""
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
        """;

    private static string? FindOutputText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("output_text", out var outputText) &&
                outputText.ValueKind == JsonValueKind.String)
                return outputText.GetString();

            if (element.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String &&
                type.GetString() == "output_text" &&
                element.TryGetProperty("text", out var text) &&
                text.ValueKind == JsonValueKind.String)
                return text.GetString();

            foreach (var property in element.EnumerateObject())
            {
                var nested = FindOutputText(property.Value);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindOutputText(item);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }

        return null;
    }

    private static int TryReadTotalTokens(JsonElement root)
    {
        if (root.TryGetProperty("usage", out var usage) &&
            usage.TryGetProperty("total_tokens", out var totalTokens) &&
            totalTokens.TryGetInt32(out var tokens))
        {
            return tokens;
        }

        return 0;
    }
}
