using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pasukhi.Application.AI;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Services;

public class OpenAiService : IAiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<OpenAiService> _logger;

    public OpenAiService(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<OpenAiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress ??= new Uri("https://api.openai.com/v1/");
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds));
    }

    public async Task<AiReplyResult> GenerateReplyAsync(AiContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Failure("AI API key is not configured.");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(BuildRequest(context), JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenAI response failed with status {StatusCode}: {Body}",
                    (int)response.StatusCode,
                    Truncate(content, 500));
                return Failure($"OpenAI returned HTTP {(int)response.StatusCode}.", stopwatch.Elapsed);
            }

            return ParseResponse(content, stopwatch.Elapsed);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "OpenAI request timed out.");
            return Failure("OpenAI request timed out.", stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "OpenAI request failed.");
            return Failure("OpenAI request failed.", stopwatch.Elapsed);
        }
    }

    private object BuildRequest(AiContext context) => new
    {
        model = string.IsNullOrWhiteSpace(_options.Model) ? _options.GetDefaultModel() : _options.Model,
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
        max_output_tokens = _options.MaxTokens,
        temperature = _options.Temperature,
        reasoning = new { effort = _options.ReasoningEffort }
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

    private static string BuildUserPrompt(AiContext context)
    {
        var faqs = context.RelevantFaqs.Count == 0
            ? "No FAQ context is available."
            : string.Join(
                "\n\n",
                context.RelevantFaqs.Select(f => $"Q: {f.Question}\nA: {f.Answer}"));

        var history = context.ConversationHistory.Count == 0
            ? "No previous messages."
            : string.Join(
                "\n",
                context.ConversationHistory.Select(m => $"{m.Role}: {m.Content}"));

        return $"""
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
        """;
    }

    private static AiReplyResult ParseResponse(string content, TimeSpan elapsed)
    {
        using var doc = JsonDocument.Parse(content);
        var outputText = FindOutputText(doc.RootElement);
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return Failure("OpenAI response did not include output text.", elapsed);
        }

        using var outputDoc = JsonDocument.Parse(outputText);
        var root = outputDoc.RootElement;
        var replyText = root.TryGetProperty("replyText", out var replyElement) && replyElement.ValueKind != JsonValueKind.Null
            ? replyElement.GetString()
            : null;
        var confidence = root.TryGetProperty("confidenceScore", out var confidenceElement) &&
                         confidenceElement.TryGetDouble(out var parsedConfidence)
            ? parsedConfidence
            : 0;
        var shouldEscalate = root.TryGetProperty("shouldEscalate", out var escalateElement) &&
                             escalateElement.ValueKind == JsonValueKind.True;
        var escalationReason = root.TryGetProperty("escalationReason", out var reasonElement) &&
                               reasonElement.ValueKind != JsonValueKind.Null
            ? reasonElement.GetString()
            : null;

        return new AiReplyResult(
            true,
            replyText,
            Math.Clamp(confidence, 0, 1),
            shouldEscalate,
            escalationReason,
            TryReadTotalTokens(doc.RootElement),
            elapsed);
    }

    private static string? FindOutputText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("output_text", out var outputText) &&
                outputText.ValueKind == JsonValueKind.String)
            {
                return outputText.GetString();
            }

            if (element.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String &&
                type.GetString() == "output_text" &&
                element.TryGetProperty("text", out var text) &&
                text.ValueKind == JsonValueKind.String)
            {
                return text.GetString();
            }

            foreach (var property in element.EnumerateObject())
            {
                var nested = FindOutputText(property.Value);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindOutputText(item);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
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

    private static AiReplyResult Failure(string error, TimeSpan? elapsed = null) =>
        new(false, null, 0, true, error, 0, elapsed ?? TimeSpan.Zero, error);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
