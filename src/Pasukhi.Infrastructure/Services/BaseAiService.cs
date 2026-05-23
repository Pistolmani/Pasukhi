using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pasukhi.Application.AI;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Services;

public abstract class BaseAiService : IAiService
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    protected readonly HttpClient HttpClient;
    protected readonly AiOptions Options;
    private readonly ILogger _logger;

    protected abstract string ProviderName { get; }

    protected BaseAiService(HttpClient httpClient, AiOptions options, ILogger logger)
    {
        HttpClient = httpClient;
        Options = options;
        _logger = logger;
        HttpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, Options.RequestTimeoutSeconds));
    }

    public async Task<AiReplyResult> GenerateReplyAsync(AiContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Options.ApiKey))
            return Failure($"{ProviderName} API key is not configured.");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = BuildHttpRequest(context);
            using var response = await HttpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "{Provider} response failed with status {StatusCode}: {Body}",
                    ProviderName, (int)response.StatusCode, Truncate(content, 500));
                return Failure($"{ProviderName} returned HTTP {(int)response.StatusCode}.", stopwatch.Elapsed);
            }

            return ParseResponse(content, stopwatch.Elapsed);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "{Provider} request timed out.", ProviderName);
            return Failure($"{ProviderName} request timed out.", stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "{Provider} request failed.", ProviderName);
            return Failure($"{ProviderName} request failed.", stopwatch.Elapsed);
        }
    }

    protected abstract HttpRequestMessage BuildHttpRequest(AiContext context);
    protected abstract AiReplyResult ParseResponse(string content, TimeSpan elapsed);

    protected static AiReplyResult ParseOutputJson(JsonElement root, int tokensUsed, TimeSpan elapsed)
    {
        var replyText = root.TryGetProperty("replyText", out var replyEl) && replyEl.ValueKind != JsonValueKind.Null
            ? replyEl.GetString() : null;
        var confidence = root.TryGetProperty("confidenceScore", out var confEl) && confEl.TryGetDouble(out var c) ? c : 0;
        var shouldEscalate = root.TryGetProperty("shouldEscalate", out var escEl) && escEl.ValueKind == JsonValueKind.True;
        var escalationReason = root.TryGetProperty("escalationReason", out var reasonEl) && reasonEl.ValueKind != JsonValueKind.Null
            ? reasonEl.GetString() : null;
        return new AiReplyResult(true, replyText, Math.Clamp(confidence, 0, 1), shouldEscalate, escalationReason, tokensUsed, elapsed);
    }

    protected static string FormatFaqs(AiContext context) =>
        context.RelevantFaqs.Count == 0
            ? "No FAQ context is available."
            : string.Join("\n\n", context.RelevantFaqs.Select(f => $"Q: {f.Question}\nA: {f.Answer}"));

    protected static string FormatHistory(AiContext context) =>
        context.ConversationHistory.Count == 0
            ? "No previous messages."
            : string.Join("\n", context.ConversationHistory.Select(m => $"{m.Role}: {m.Content}"));

    protected static AiReplyResult Failure(string error, TimeSpan? elapsed = null) =>
        new(false, null, 0, true, error, 0, elapsed ?? TimeSpan.Zero, error);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
