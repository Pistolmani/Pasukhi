using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pasukhi.Application.AI;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Services;

namespace Pasukhi.UnitTests.Services;

public class GeminiServiceTests
{
    [Fact]
    public async Task GenerateReplyAsync_returns_failure_when_api_key_missing()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Should not call Gemini."));
        var service = NewService(handler, new AiOptions { ApiKey = "" });

        var result = await service.GenerateReplyAsync(NewContext());

        Assert.False(result.Success);
        Assert.Equal("Gemini API key is not configured.", result.Error);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GenerateReplyAsync_posts_request_and_parses_structured_json()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "candidates": [{
                    "content": {
                      "parts": [{
                        "text": "{\"replyText\":\"Sure, we can help.\",\"confidenceScore\":0.91,\"shouldEscalate\":false,\"escalationReason\":null}"
                      }]
                    }
                  }],
                  "usageMetadata": { "candidatesTokenCount": 42 }
                }
                """)
        });
        var service = NewService(handler, new AiOptions { ApiKey = "test-key", Model = "gemini-2.0-flash-lite" });

        var result = await service.GenerateReplyAsync(NewContext());

        Assert.True(result.Success);
        Assert.Equal("Sure, we can help.", result.ReplyText);
        Assert.Equal(0.91, result.ConfidenceScore);
        Assert.False(result.ShouldEscalate);
        Assert.Equal(42, result.TokensUsed);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Contains("models/gemini-2.0-flash-lite:generateContent", handler.LastRequest?.RequestUri?.ToString());
        Assert.True(handler.LastRequest?.Headers.Contains("x-goog-api-key"));
        Assert.Contains("\"responseMimeType\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task GenerateReplyAsync_returns_failure_for_bad_status()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"error":"boom"}""")
        });
        var service = NewService(handler);

        var result = await service.GenerateReplyAsync(NewContext());

        Assert.False(result.Success);
        Assert.Equal("Gemini returned HTTP 500.", result.Error);
    }

    [Fact]
    public async Task GenerateReplyAsync_returns_failure_when_candidates_missing()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"candidates":[]}""")
        });
        var service = NewService(handler);

        var result = await service.GenerateReplyAsync(NewContext());

        Assert.False(result.Success);
        Assert.Equal("Gemini response did not include any candidates.", result.Error);
    }

    [Fact]
    public async Task GenerateReplyAsync_returns_failure_for_malformed_json()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"candidates":[{"content":{"parts":[{"text":"{not-json"}]}}]}""")
        });
        var service = NewService(handler);

        var result = await service.GenerateReplyAsync(NewContext());

        Assert.False(result.Success);
        Assert.Equal("Gemini request failed.", result.Error);
    }

    [Fact]
    public async Task GenerateReplyAsync_returns_failure_for_timeout()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("timeout"));
        var service = NewService(handler);

        var result = await service.GenerateReplyAsync(NewContext());

        Assert.False(result.Success);
        Assert.Equal("Gemini request timed out.", result.Error);
    }

    private static GeminiService NewService(
        StubHttpMessageHandler handler,
        AiOptions? options = null) =>
        new(
            new HttpClient(handler),
            Options.Create(options ?? new AiOptions { ApiKey = "test-key", Model = "gemini-2.0-flash-lite" }),
            NullLogger<GeminiService>.Instance);

    private static AiContext NewContext() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Pasukhi Test",
        "A test business.",
        "Answer only from context.",
        "friendly",
        "Let me connect you with our team.",
        true,
        50_000,
        0.7,
        ChannelType.Instagram,
        "Customer",
        "Can you help?",
        Array.Empty<AiFaqContextItem>(),
        Array.Empty<AiMessage>());

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public int CallCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _handler(request);
        }
    }
}
