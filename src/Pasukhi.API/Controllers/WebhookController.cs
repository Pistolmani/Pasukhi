using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pasukhi.API.Webhooks;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookSignatureVerifier _verifier;
    private readonly IWebhookResolver _resolver;
    private readonly IPublishEndpoint _bus;
    private readonly PasukhiDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IWebhookSignatureVerifier verifier,
        IWebhookResolver resolver,
        IPublishEndpoint bus,
        PasukhiDbContext db,
        IConfiguration config,
        ILogger<WebhookController> logger)
    {
        _verifier = verifier;
        _resolver = resolver;
        _bus = bus;
        _db = db;
        _config = config;
        _logger = logger;
    }

    // ── Verification (Meta sends GET to confirm the endpoint) ──────────────

    [HttpGet("instagram")]
    [HttpGet("messenger")]
    [HttpGet("whatsapp")]
    public async Task<IActionResult> Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        CancellationToken ct)
    {
        if (mode != "subscribe" || string.IsNullOrEmpty(token))
            return BadRequest();

        var valid = await _db.ChannelConnections
            .IgnoreQueryFilters()
            .AnyAsync(c => c.VerifyToken == token && c.IsActive, ct);

        if (!valid)
        {
            _logger.LogWarning("Webhook verification failed for token {Token}", token);
            return Forbid();
        }

        return Ok(challenge);
    }

    // ── Instagram & Messenger ───────────────────────────────────────────────

    [HttpPost("instagram")]
    public Task<IActionResult> InstagramAsync() =>
        HandleMetaPageAsync(ChannelType.Instagram);

    [HttpPost("messenger")]
    public Task<IActionResult> MessengerAsync() =>
        HandleMetaPageAsync(ChannelType.Messenger);

    // ── WhatsApp ────────────────────────────────────────────────────────────

    [HttpPost("whatsapp")]
    public Task<IActionResult> WhatsAppAsync() =>
        HandleWhatsAppAsync();

    // ── Internals ───────────────────────────────────────────────────────────

    private async Task<IActionResult> HandleMetaPageAsync(ChannelType channelType)
    {
        var (body, signature) = await ReadBodyAndSignatureAsync();
        if (body is null) return BadRequest();

        var appSecret = _config["Meta:AppSecret"] ?? string.Empty;
        if (!_verifier.Verify(body, signature, appSecret))
        {
            _logger.LogWarning("Invalid signature for {Channel} webhook", channelType);
            return Unauthorized();
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

        foreach (var entry in payload.Entry)
        {
            if (entry.Messaging is null) continue;

            var channel = await _resolver.ResolveAsync(entry.Id, channelType);
            if (channel is null)
            {
                _logger.LogWarning("No active {Channel} channel for ExternalAccountId={Id}", channelType, entry.Id);
                continue;
            }

            foreach (var msg in entry.Messaging)
            {
                if (msg.Message is null) continue;
                if (msg.Message.IsEcho == true) continue;

                var evt = BuildPageEvent(channel.BusinessId, channel.Id, channelType, msg, body);
                await _bus.Publish(evt);
            }

            await UpdateLastWebhookAsync(channel.Id);
        }

        return Ok();
    }

    private async Task<IActionResult> HandleWhatsAppAsync()
    {
        var (body, signature) = await ReadBodyAndSignatureAsync();
        if (body is null) return BadRequest();

        var appSecret = _config["Meta:AppSecret"] ?? string.Empty;
        if (!_verifier.Verify(body, signature, appSecret))
        {
            _logger.LogWarning("Invalid signature for WhatsApp webhook");
            return Unauthorized();
        }

        MetaWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<MetaWebhookPayload>(body);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize WhatsApp webhook payload");
            return Ok();
        }

        if (payload is null) return Ok();

        foreach (var entry in payload.Entry)
        {
            if (entry.Changes is null) continue;

            foreach (var change in entry.Changes)
            {
                if (change.Field != "messages" || change.Value?.Messages is null) continue;

                var phoneNumberId = change.Value.Metadata?.PhoneNumberId ?? string.Empty;
                var channel = await _resolver.ResolveAsync(phoneNumberId, ChannelType.WhatsApp);
                if (channel is null)
                {
                    _logger.LogWarning("No active WhatsApp channel for PhoneNumberId={Id}", phoneNumberId);
                    continue;
                }

                var contactMap = change.Value.Contacts?
                    .ToDictionary(c => c.WaId, c => c.Profile?.Name)
                    ?? new Dictionary<string, string?>();

                foreach (var msg in change.Value.Messages)
                {
                    var evt = BuildWhatsAppEvent(channel.BusinessId, channel.Id, msg, contactMap, body);
                    await _bus.Publish(evt);
                }

                await UpdateLastWebhookAsync(channel.Id);
            }
        }

        return Ok();
    }

    private static InboundMessageEvent BuildPageEvent(
        Guid businessId,
        Guid channelConnectionId,
        ChannelType channelType,
        MetaMessagingEvent msg,
        string rawBody)
    {
        var (msgType, mediaUrl, mimeType) = ExtractAttachment(msg.Message?.Attachments);

        return new InboundMessageEvent
        {
            BusinessId = businessId,
            ChannelConnectionId = channelConnectionId,
            ChannelType = channelType.ToString(),
            ExternalSenderId = msg.Sender.Id,
            ExternalMessageId = msg.Message?.Mid ?? string.Empty,
            TextContent = msg.Message?.Text,
            MediaUrl = mediaUrl,
            MediaMimeType = mimeType,
            MessageType = msgType,
            ExternalTimestamp = msg.Timestamp.ToString(),
            RawPayloadJson = rawBody
        };
    }

    private static InboundMessageEvent BuildWhatsAppEvent(
        Guid businessId,
        Guid channelConnectionId,
        MetaWhatsAppMessage msg,
        Dictionary<string, string?> contactMap,
        string rawBody)
    {
        var msgType = msg.Type switch
        {
            "image" => "Image",
            "audio" => "Audio",
            "video" => "Video",
            "document" => "File",
            _ => "Text"
        };

        var mediaUrl = msg.Image?.Url ?? msg.Audio?.Url ?? msg.Video?.Url ?? msg.Document?.Url;
        var mimeType = msg.Image?.MimeType ?? msg.Audio?.MimeType ?? msg.Video?.MimeType ?? msg.Document?.MimeType;

        contactMap.TryGetValue(msg.From, out var displayName);

        return new InboundMessageEvent
        {
            BusinessId = businessId,
            ChannelConnectionId = channelConnectionId,
            ChannelType = "WhatsApp",
            ExternalSenderId = msg.From,
            SenderDisplayName = displayName,
            ExternalMessageId = msg.Id,
            TextContent = msg.Text?.Body,
            MediaUrl = mediaUrl,
            MediaMimeType = mimeType,
            MessageType = msgType,
            ExternalTimestamp = msg.Timestamp,
            RawPayloadJson = rawBody
        };
    }

    private static (string type, string? url, string? mime) ExtractAttachment(List<MetaAttachment>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
            return ("Text", null, null);

        var a = attachments[0];
        var type = a.Type switch
        {
            "image" => "Image",
            "audio" => "Audio",
            "video" => "Video",
            "file" => "File",
            _ => "Text"
        };

        return (type, a.Payload?.Url, a.Payload?.MimeType);
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

    private async Task UpdateLastWebhookAsync(Guid channelId)
    {
        try
        {
            await _db.ChannelConnections
                .IgnoreQueryFilters()
                .Where(c => c.Id == channelId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastWebhookAt, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update LastWebhookAt for channel {ChannelId}", channelId);
        }
    }
}
