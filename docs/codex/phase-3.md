# Codex Task — Phase 3: Webhooks, Signature Verification, and Meta Integration

> Read `AGENTS.md` first. Phases 0, 1, and 2 must be complete before starting this.

## Goal

By the end of this phase:
- Meta webhook verification (GET) works for all three channels
- Inbound webhook payloads (POST) are verified via HMAC-SHA256 signature
- Verified payloads are parsed into a normalized `InboundMessageEvent` and published to RabbitMQ
- The webhook controller returns 200 immediately — no DB writes, no AI calls
- A placeholder MassTransit consumer logs received events (full processing comes in Phase 4)
- ngrok is configured so Meta can reach your local endpoint
- The frontend has a Webhook Settings page showing the verify token per channel

---

## Repo root

`C:\Users\piros\OneDrive\Desktop\Pasukhi\`

---

## Step 1 — MassTransit Message Contract

### `src/Pasukhi.Application/Messaging/InboundMessageEvent.cs`

```csharp
namespace Pasukhi.Application.Messaging;

public record InboundMessageEvent
{
    public Guid BusinessId { get; init; }
    public Guid ChannelConnectionId { get; init; }
    public string ChannelType { get; init; } = string.Empty;   // "Instagram" | "Messenger" | "WhatsApp"
    public string ExternalSenderId { get; init; } = string.Empty;
    public string? SenderDisplayName { get; init; }
    public string ExternalMessageId { get; init; } = string.Empty;
    public string? TextContent { get; init; }
    public string? MediaUrl { get; init; }
    public string? MediaMimeType { get; init; }
    public string MessageType { get; init; } = "Text";         // "Text" | "Image" | "Audio" | "Video" | "File"
    public string ExternalTimestamp { get; init; } = string.Empty;
    public string RawPayloadJson { get; init; } = string.Empty;
}
```

---

## Step 2 — Webhook Payload DTOs

These are used only for deserialization inside the webhook controller — they never leave the API layer.

### `src/Pasukhi.API/Webhooks/MetaWebhookPayload.cs`

```csharp
using System.Text.Json.Serialization;

namespace Pasukhi.API.Webhooks;

public record MetaWebhookPayload
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = string.Empty;

    [JsonPropertyName("entry")]
    public List<MetaEntry> Entry { get; init; } = new();
}

public record MetaEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("messaging")]
    public List<MetaMessagingEvent>? Messaging { get; init; }

    [JsonPropertyName("changes")]
    public List<MetaChange>? Changes { get; init; }
}

public record MetaMessagingEvent
{
    [JsonPropertyName("sender")]
    public MetaParticipant Sender { get; init; } = new();

    [JsonPropertyName("recipient")]
    public MetaParticipant Recipient { get; init; } = new();

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    [JsonPropertyName("message")]
    public MetaMessage? Message { get; init; }
}

public record MetaParticipant
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}

public record MetaMessage
{
    [JsonPropertyName("mid")]
    public string Mid { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("attachments")]
    public List<MetaAttachment>? Attachments { get; init; }
}

public record MetaAttachment
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("payload")]
    public MetaAttachmentPayload? Payload { get; init; }
}

public record MetaAttachmentPayload
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }
}

public record MetaChange
{
    [JsonPropertyName("value")]
    public MetaChangeValue? Value { get; init; }

    [JsonPropertyName("field")]
    public string Field { get; init; } = string.Empty;
}

public record MetaChangeValue
{
    [JsonPropertyName("messaging_product")]
    public string? MessagingProduct { get; init; }

    [JsonPropertyName("metadata")]
    public MetaWhatsAppMetadata? Metadata { get; init; }

    [JsonPropertyName("contacts")]
    public List<MetaWhatsAppContact>? Contacts { get; init; }

    [JsonPropertyName("messages")]
    public List<MetaWhatsAppMessage>? Messages { get; init; }
}

public record MetaWhatsAppMetadata
{
    [JsonPropertyName("display_phone_number")]
    public string? DisplayPhoneNumber { get; init; }

    [JsonPropertyName("phone_number_id")]
    public string? PhoneNumberId { get; init; }
}

public record MetaWhatsAppContact
{
    [JsonPropertyName("profile")]
    public MetaWhatsAppProfile? Profile { get; init; }

    [JsonPropertyName("wa_id")]
    public string WaId { get; init; } = string.Empty;
}

public record MetaWhatsAppProfile
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public record MetaWhatsAppMessage
{
    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    public MetaWhatsAppText? Text { get; init; }

    [JsonPropertyName("image")]
    public MetaWhatsAppMedia? Image { get; init; }

    [JsonPropertyName("audio")]
    public MetaWhatsAppMedia? Audio { get; init; }

    [JsonPropertyName("video")]
    public MetaWhatsAppMedia? Video { get; init; }

    [JsonPropertyName("document")]
    public MetaWhatsAppMedia? Document { get; init; }
}

public record MetaWhatsAppText
{
    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;
}

public record MetaWhatsAppMedia
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}
```

---

## Step 3 — Signature Verification Service

### `src/Pasukhi.Application/Interfaces/IWebhookSignatureVerifier.cs`

```csharp
namespace Pasukhi.Application.Interfaces;

public interface IWebhookSignatureVerifier
{
    bool Verify(string payload, string signatureHeader, string appSecret);
}
```

### `src/Pasukhi.Infrastructure/Services/WebhookSignatureVerifier.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Services;

public class WebhookSignatureVerifier : IWebhookSignatureVerifier
{
    public bool Verify(string payload, string signatureHeader, string appSecret)
    {
        // signatureHeader format: "sha256=<hex>"
        if (!signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        var receivedHash = signatureHeader["sha256=".Length..];

        var keyBytes = Encoding.UTF8.GetBytes(appSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var computedHash = hmac.ComputeHash(payloadBytes);
        var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(receivedHash.ToLowerInvariant())
        );
    }
}
```

---

## Step 4 — Webhook Resolver Service

Looks up the `ChannelConnection` by `ExternalAccountId` and returns the `BusinessId`.

### `src/Pasukhi.Application/Interfaces/IWebhookResolver.cs`

```csharp
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Interfaces;

public interface IWebhookResolver
{
    Task<ChannelConnection?> ResolveAsync(string externalAccountId, ChannelType channelType, CancellationToken ct = default);
}
```

### `src/Pasukhi.Infrastructure/Services/WebhookResolver.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class WebhookResolver : IWebhookResolver
{
    private readonly PasukhiDbContext _db;

    public WebhookResolver(PasukhiDbContext db)
    {
        _db = db;
    }

    public async Task<ChannelConnection?> ResolveAsync(string externalAccountId, ChannelType channelType, CancellationToken ct = default)
    {
        return await _db.ChannelConnections
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.ExternalAccountId == externalAccountId &&
                c.ChannelType == channelType &&
                c.IsActive,
                ct);
    }
}
```

---

## Step 5 — MassTransit Placeholder Consumer

### `src/Pasukhi.Infrastructure/Consumers/InboundMessageConsumer.cs`

```csharp
using MassTransit;
using Microsoft.Extensions.Logging;
using Pasukhi.Application.Messaging;

namespace Pasukhi.Infrastructure.Consumers;

public class InboundMessageConsumer : IConsumer<InboundMessageEvent>
{
    private readonly ILogger<InboundMessageConsumer> _logger;

    public InboundMessageConsumer(ILogger<InboundMessageConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<InboundMessageEvent> context)
    {
        var e = context.Message;
        _logger.LogInformation(
            "InboundMessage received | Business={BusinessId} | Channel={ChannelType} | Sender={SenderId} | MessageId={MessageId}",
            e.BusinessId, e.ChannelType, e.ExternalSenderId, e.ExternalMessageId);

        // Full processing wired in Phase 4
        return Task.CompletedTask;
    }
}
```

---

## Step 6 — Webhook Controller

### `src/Pasukhi.API/Controllers/WebhookController.cs`

```csharp
using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.API.Webhooks;
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
    private readonly IConfiguration _config;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IWebhookSignatureVerifier verifier,
        IWebhookResolver resolver,
        IPublishEndpoint bus,
        IConfiguration config,
        ILogger<WebhookController> logger)
    {
        _verifier = verifier;
        _resolver = resolver;
        _bus = bus;
        _config = config;
        _logger = logger;
    }

    // ── Verification (Meta sends GET to confirm the endpoint) ──────────────

    [HttpGet("instagram")]
    [HttpGet("messenger")]
    [HttpGet("whatsapp")]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string token,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        if (mode != "subscribe")
            return BadRequest();

        // Find a ChannelConnection whose VerifyToken matches
        // We accept if any active channel has this token
        var valid = HttpContext.RequestServices
            .GetRequiredService<PasukhiDbContext>()
            .ChannelConnections
            .IgnoreQueryFilters()
            .Any(c => c.VerifyToken == token && c.IsActive);

        if (!valid)
        {
            _logger.LogWarning("Webhook verification failed for token {Token}", token);
            return Forbid();
        }

        return Ok(challenge);
    }

    // ── Instagram & Messenger (same payload structure) ──────────────────────

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

        var payload = JsonSerializer.Deserialize<MetaWebhookPayload>(body);
        if (payload is null) return Ok(); // return 200 even on parse failure

        foreach (var entry in payload.Entry)
        {
            if (entry.Messaging is null) continue;

            var channel = await _resolver.ResolveAsync(entry.Id, channelType);
            if (channel is null)
            {
                _logger.LogWarning("No active channel found for ExternalAccountId={Id}", entry.Id);
                continue;
            }

            foreach (var msg in entry.Messaging)
            {
                if (msg.Message is null) continue;
                if (msg.Message.Mid.StartsWith("mid.$")) continue; // echo suppression

                var evt = BuildPageEvent(channel.BusinessId, channel.Id, channelType, msg, body);
                await _bus.Publish(evt);
            }

            // Update LastWebhookAt (fire-and-forget — do not await DB in hot path)
            _ = UpdateLastWebhookAsync(channel.Id);
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

        var payload = JsonSerializer.Deserialize<MetaWebhookPayload>(body);
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

                _ = UpdateLastWebhookAsync(channel.Id);
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
            using var scope = HttpContext.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
            await db.ChannelConnections
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
```

---

## Step 7 — Register Services in Program.cs

Add inside the service registration section (before `builder.Build()`):

```csharp
// Webhook services
builder.Services.AddScoped<IWebhookSignatureVerifier, WebhookSignatureVerifier>();
builder.Services.AddScoped<IWebhookResolver, WebhookResolver>();
```

Add the MassTransit consumer registration inside the existing `AddMassTransit` call:

```csharp
x.AddConsumer<InboundMessageConsumer>();
```

---

## Step 8 — appsettings Configuration

Add to `src/Pasukhi.API/appsettings.json`:

```json
"Meta": {
  "AppSecret": "",
  "AppId": ""
}
```

Add to `src/Pasukhi.API/appsettings.Development.json` (fill in your real values from Meta App Dashboard):

```json
"Meta": {
  "AppSecret": "YOUR_META_APP_SECRET",
  "AppId": "YOUR_META_APP_ID"
}
```

> Do NOT commit real secrets. Add `appsettings.Development.json` to `.gitignore` if it isn't already.

---

## Step 9 — Frontend: Webhook Settings Page

### `pasukhi-admin/src/pages/WebhookSettings.tsx`

```tsx
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { useAuthStore } from '@/store/auth';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Copy } from 'lucide-react';
import { toast } from 'sonner';

interface Channel {
  id: string;
  channelType: string;
  externalAccountName: string | null;
  verifyToken: string;
  isActive: boolean;
  lastWebhookAt: string | null;
}

const WEBHOOK_URLS: Record<string, string> = {
  Instagram: '/api/webhooks/instagram',
  Messenger: '/api/webhooks/messenger',
  WhatsApp: '/api/webhooks/whatsapp',
};

export default function WebhookSettings() {
  const { data: channels = [] } = useQuery<Channel[]>({
    queryKey: ['channels'],
    queryFn: () => api.get('/api/channels').then(r => r.data),
  });

  const ngrokBase = import.meta.env.VITE_NGROK_URL ?? 'https://your-ngrok-url.ngrok.io';

  function copy(text: string) {
    navigator.clipboard.writeText(text);
    toast.success('Copied to clipboard');
  }

  return (
    <div className="p-6 space-y-6">
      <h1 className="text-2xl font-semibold">Webhook Settings</h1>
      <p className="text-muted-foreground text-sm">
        Use these details when configuring your Meta App webhooks.
      </p>

      {channels.map(ch => {
        const path = WEBHOOK_URLS[ch.channelType] ?? '';
        const fullUrl = `${ngrokBase}${path}`;
        return (
          <Card key={ch.id}>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                {ch.channelType}
                {ch.externalAccountName && (
                  <span className="font-normal text-muted-foreground">— {ch.externalAccountName}</span>
                )}
                <Badge variant={ch.isActive ? 'default' : 'secondary'}>
                  {ch.isActive ? 'Active' : 'Inactive'}
                </Badge>
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <div className="flex items-center justify-between rounded bg-muted px-3 py-2">
                <span className="font-mono text-xs break-all">{fullUrl}</span>
                <button onClick={() => copy(fullUrl)} className="ml-2 shrink-0">
                  <Copy className="h-4 w-4 text-muted-foreground hover:text-foreground" />
                </button>
              </div>
              <div className="flex items-center justify-between rounded bg-muted px-3 py-2">
                <span className="text-muted-foreground mr-2 shrink-0">Verify Token:</span>
                <span className="font-mono text-xs break-all">{ch.verifyToken}</span>
                <button onClick={() => copy(ch.verifyToken)} className="ml-2 shrink-0">
                  <Copy className="h-4 w-4 text-muted-foreground hover:text-foreground" />
                </button>
              </div>
              {ch.lastWebhookAt && (
                <p className="text-xs text-muted-foreground">
                  Last webhook received: {new Date(ch.lastWebhookAt).toLocaleString()}
                </p>
              )}
            </CardContent>
          </Card>
        );
      })}

      {channels.length === 0 && (
        <p className="text-muted-foreground text-sm">No channels configured yet. Add channels first.</p>
      )}
    </div>
  );
}
```

Add route in your router:

```tsx
<Route path="/webhook-settings" element={<WebhookSettings />} />
```

Add nav link (wherever your sidebar links live):

```tsx
{ label: 'Webhook Settings', path: '/webhook-settings', icon: Webhook }
```

Add to `.env` (or `.env.local`):

```
VITE_NGROK_URL=https://your-ngrok-url.ngrok.io
```

---

## Step 10 — ngrok Setup

Install ngrok if not already:

```bash
winget install ngrok
```

Start your API first:

```bash
dotnet run --project src/Pasukhi.API
```

In a second terminal, expose port 5000:

```bash
ngrok http 5000
```

Copy the `https://` forwarding URL (e.g. `https://abc123.ngrok.io`) and:
1. Paste it into `VITE_NGROK_URL` in your frontend `.env`
2. Use it in the Meta App Dashboard as your webhook callback URL

---

## Verification

### Backend build
```bash
cd "C:\Users\piros\OneDrive\Desktop\Pasukhi"
dotnet build
```
Expected: 0 errors.

### Frontend typecheck + build
```bash
cd pasukhi-admin
npx tsc --noEmit
npm run build
```
Expected: 0 errors.

### Webhook verification test (with ngrok running)
```powershell
$ngrok = "https://your-ngrok-url.ngrok.io"
$verifyToken = "token-from-your-channel-connection"

Invoke-RestMethod "$ngrok/api/webhooks/instagram?hub.mode=subscribe&hub.verify_token=$verifyToken&hub.challenge=testchallenge123"
# Expected: testchallenge123
```

### Simulate inbound message (Instagram)
```powershell
# Replace with real values from your Meta App
$body = @{
    object = "instagram"
    entry = @(@{
        id = "YOUR_PAGE_ID"
        messaging = @(@{
            sender = @{ id = "123456789" }
            recipient = @{ id = "YOUR_PAGE_ID" }
            timestamp = 1700000000
            message = @{ mid = "m_test123"; text = "Hello!" }
        })
    })
} | ConvertTo-Json -Depth 10

# Note: signature verification will fail without a real HMAC — disable it temporarily in dev
# by returning true from WebhookSignatureVerifier when AppSecret is empty
Invoke-RestMethod -Method Post -Uri "$ngrok/api/webhooks/instagram" -Body $body -ContentType "application/json"
# Expected: 200 OK, and the log shows "InboundMessage received"
```

---

## Commit

```bash
git add src/ pasukhi-admin/ docs/codex/phase-3.md
git commit -m "feat(03-01): webhook controllers, signature verification, and Meta integration"
```

---

## What's Next

Phase 4: `docs/codex/phase-4.md` — MassTransit consumers, RabbitMQ wiring, conversation + message persistence, and full inbound message pipeline.
