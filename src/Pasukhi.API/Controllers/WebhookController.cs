using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Application.Webhooks;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/webhooks")]
[EnableRateLimiting("webhook")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookSignatureVerifier _verifier;
    private readonly IWebhookResolver _resolver;
    private readonly IMetaWebhookParser _parser;
    private readonly IInboundMessageEnqueuer _inboundEnqueuer;
    private readonly PasukhiDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IWebhookSignatureVerifier verifier,
        IWebhookResolver resolver,
        IMetaWebhookParser parser,
        IInboundMessageEnqueuer inboundEnqueuer,
        PasukhiDbContext db,
        IConfiguration config,
        ILogger<WebhookController> logger)
    {
        _verifier = verifier;
        _resolver = resolver;
        _parser = parser;
        _inboundEnqueuer = inboundEnqueuer;
        _db = db;
        _config = config;
        _logger = logger;
    }

    [HttpGet("instagram")]
    public Task<IActionResult> VerifyInstagram(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        CancellationToken ct) =>
        VerifyAsync(ChannelType.Instagram, mode, token, challenge, ct);

    [HttpGet("messenger")]
    public Task<IActionResult> VerifyMessenger(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        CancellationToken ct) =>
        VerifyAsync(ChannelType.Messenger, mode, token, challenge, ct);

    [HttpGet("whatsapp")]
    public Task<IActionResult> VerifyWhatsApp(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        CancellationToken ct) =>
        VerifyAsync(ChannelType.WhatsApp, mode, token, challenge, ct);

    private async Task<IActionResult> VerifyAsync(
        ChannelType channelType,
        string? mode,
        string? token,
        string? challenge,
        CancellationToken ct)
    {
        if (mode != "subscribe" || string.IsNullOrEmpty(token))
            return BadRequest();

        var channel = await _db.ChannelConnections
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.VerifyToken == token &&
                c.ChannelType == channelType &&
                c.IsActive, ct);

        if (channel is null)
        {
            _logger.LogWarning("Webhook verification failed for {Channel} token {Token}", channelType, token);
            return Forbid();
        }

        return Ok(challenge);
    }

    [HttpPost("instagram")]
    public Task<IActionResult> InstagramAsync(CancellationToken ct) =>
        HandleWebhookAsync(ChannelType.Instagram, _parser.ParseInstagramBatch, ct);

    [HttpPost("messenger")]
    public Task<IActionResult> MessengerAsync(CancellationToken ct) =>
        HandleWebhookAsync(ChannelType.Messenger, _parser.ParseMessengerBatch, ct);

    [HttpPost("whatsapp")]
    public Task<IActionResult> WhatsAppAsync(CancellationToken ct) =>
        HandleWebhookAsync(ChannelType.WhatsApp, _parser.ParseWhatsAppBatch, ct);

    private async Task<IActionResult> HandleWebhookAsync(
        ChannelType channelType,
        Func<MetaWebhookPayload, IReadOnlyList<InboundMessageEvent>> parse,
        CancellationToken ct)
    {
        var (body, signature) = await ReadBodyAndSignatureAsync();
        if (body is null) return BadRequest();

        var appSecret = _config["Meta:AppSecret"] ?? string.Empty;
        if (!_verifier.Verify(body, signature, appSecret))
        {
            _logger.LogWarning("Invalid signature for {Channel} webhook", channelType);
            return Forbid();
        }

        MetaWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<MetaWebhookPayload>(body);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize {Channel} webhook payload", channelType);
            return Ok();
        }

        if (payload is null) return Ok();

        var parsedEvents = parse(payload);
        if (parsedEvents.Count == 0)
        {
            _logger.LogDebug("No inbound message found in {Channel} webhook payload.", channelType);
            return Ok();
        }

        foreach (var parsed in parsedEvents)
        {
            var channel = await _resolver.ResolveAsync(parsed.ExternalAccountId, channelType, ct);
            if (channel is null)
            {
                _logger.LogWarning(
                    "No active {Channel} channel for ExternalAccountId={ExternalAccountId}",
                    channelType,
                    parsed.ExternalAccountId);
                continue;
            }

            var evt = parsed with
            {
                BusinessId = channel.BusinessId,
                ChannelConnectionId = channel.Id,
                RawPayloadJson = body
            };

            // Non-blocking enqueue. With DropOldest mode the channel makes room
            // by evicting the oldest event if it is full; Meta retries ensure delivery.
            _inboundEnqueuer.TryEnqueue(evt);
        }

        return Ok();
    }

    private async Task<(string? body, string signature)> ReadBodyAndSignatureAsync()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault() ?? string.Empty;
        return (string.IsNullOrWhiteSpace(body) ? null : body, signature);
    }
}
