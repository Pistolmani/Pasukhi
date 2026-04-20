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

    public GeminiService(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress ??= new Uri("https://generativelanguage.googleapis.com/v1beta/");
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds));
    }

    public async Task<AiReplyResult> GenerateReplyAsync(AiContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Failure("Gemini API key is not configured.");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var model = _options.GetDefaultModel();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"models/{model}:generateContent");
            request.Headers.Add("x-goog-api-key", _options.ApiKey);
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
                    "Gemini response failed with status {StatusCode}: {Body}",
                    (int)response.StatusCode,
                    Truncate(content, 500));
                return Failure($"Gemini returned HTTP {(int)response.StatusCode}.", stopwatch.Elapsed);
            }

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
        contents = new[]
        {
            new
            {
                parts = new[]
                {
                    new
                    {
                        text = BuildFullPrompt(context)
                    }
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

        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.GetArrayLength() == 0)
        {
            return Failure("Gemini response did not include any candidates.", elapsed);
        }

        var firstCandidate = candidates[0];
        if (!firstCandidate.TryGetProperty("content", out var contentElement) ||
            !contentElement.TryGetProperty("parts", out var parts) ||
            parts.GetArrayLength() == 0)
        {
            return Failure("Gemini response did not include content parts.", elapsed);
        }

        var part = parts[0];
        if (!part.TryGetProperty("text", out var outputText) ||
            string.IsNullOrWhiteSpace(outputText.GetString()))
        {
            return Failure("Gemini response did not include output text.", elapsed);
        }

        using var outputDoc = JsonDocument.Parse(outputText.GetString()!);
        var replyRoot = outputDoc.RootElement;

        var replyText = replyRoot.TryGetProperty("replyText", out var replyElement) &&
                        replyElement.ValueKind != JsonValueKind.Null
            ? replyElement.GetString()
            : null;

        var confidence = replyRoot.TryGetProperty("confidenceScore", out var confidenceElement) &&
                         confidenceElement.TryGetDouble(out var parsedConfidence)
            ? parsedConfidence
            : 0;

        var shouldEscalate = replyRoot.TryGetProperty("shouldEscalate", out var escalateElement) &&
                             escalateElement.ValueKind == JsonValueKind.True;

        var escalationReason = replyRoot.TryGetProperty("escalationReason", out var reasonElement) &&
                               reasonElement.ValueKind != JsonValueKind.Null
            ? reasonElement.GetString()
            : null;

        var tokensUsed = TryReadTotalTokens(doc.RootElement);

        return new AiReplyResult(
            true,
            replyText,
            Math.Clamp(confidence, 0, 1),
            shouldEscalate,
            escalationReason,
            tokensUsed,
            elapsed);
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

    private static AiReplyResult Failure(string error, TimeSpan? elapsed = null) =>
        new(false, null, 0, true, error, 0, elapsed ?? TimeSpan.Zero, error);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
