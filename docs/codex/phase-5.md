# Codex Task — Phase 5: Outbound Message Pipeline + Channel Providers

> Read `AGENTS.md` first. Phases 0–4 must be complete before starting this.

## Goal

By the end of this phase:
- `OutboundMessageReadyEvent` carries a persisted outbound message to the channel provider
- `OutboundMessageConsumer` receives the event, calls the correct Meta API, and updates `DeliveryStatus`
- Three channel providers exist: `InstagramChannelProvider`, `MessengerChannelProvider`, `WhatsAppChannelProvider`
- Operators can manually send replies via `POST /api/conversations/{id}/messages`

---

## Repo root

`C:\Users\piros\OneDrive\Desktop\Pasukhi\`

---

## Step 1 — OutboundMessageReadyEvent

### `src/Pasukhi.Application/Messaging/OutboundMessageReadyEvent.cs`

```csharp
namespace Pasukhi.Application.Messaging;

public record OutboundMessageReadyEvent : ITenantScopedEvent
{
    public Guid BusinessId { get; init; }
    public Guid MessageId { get; init; }
    public Guid ConversationId { get; init; }
    public Guid ChannelConnectionId { get; init; }
    public string ChannelType { get; init; } = string.Empty;
    public string ExternalCustomerId { get; init; } = string.Empty;
    public string? TextContent { get; init; }
}
```

---

## Step 2 — Channel Provider Interfaces

### `src/Pasukhi.Application/Interfaces/IInstagramChannelProvider.cs`

```csharp
namespace Pasukhi.Application.Interfaces;

public interface IInstagramChannelProvider
{
    Task<string> SendMessageAsync(
        string externalCustomerId,
        string? text,
        string accessToken,
        CancellationToken ct = default);
}
```

### `src/Pasukhi.Application/Interfaces/IMessengerChannelProvider.cs`

```csharp
namespace Pasukhi.Application.Interfaces;

public interface IMessengerChannelProvider
{
    Task<string> SendMessageAsync(
        string externalCustomerId,
        string? text,
        string accessToken,
        CancellationToken ct = default);
}
```

### `src/Pasukhi.Application/Interfaces/IWhatsAppChannelProvider.cs`

```csharp
namespace Pasukhi.Application.Interfaces;

public interface IWhatsAppChannelProvider
{
    Task<string> SendMessageAsync(
        string externalCustomerId,
        string? text,
        string accessToken,
        string phoneNumberId,
        CancellationToken ct = default);
}
```

---

## Step 3 — Channel Provider Implementations

### `src/Pasukhi.Infrastructure/Channels/InstagramChannelProvider.cs`

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Channels;

public class InstagramChannelProvider : IInstagramChannelProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _graphBaseUrl;
    private readonly string _graphApiVersion;

    public InstagramChannelProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _graphBaseUrl = (configuration["Meta:GraphBaseUrl"] ?? "https://graph.facebook.com").TrimEnd('/');
        _graphApiVersion = configuration["Meta:GraphApiVersion"] ?? "v21.0";
    }

    public async Task<string> SendMessageAsync(
        string externalCustomerId,
        string? text,
        string accessToken,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_graphBaseUrl}/{_graphApiVersion}/me/messages")
        {
            Content = JsonContent.Create(new
            {
                recipient = new { id = externalCustomerId },
                messaging_type = "RESPONSE",
                message = new { text = text ?? string.Empty }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(content);
        return json.RootElement.TryGetProperty("message_id", out var id)
            ? id.GetString() ?? string.Empty
            : throw new InvalidOperationException("Meta response did not include message_id.");
    }
}
```

### `src/Pasukhi.Infrastructure/Channels/MessengerChannelProvider.cs`

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Channels;

public class MessengerChannelProvider : IMessengerChannelProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _graphBaseUrl;
    private readonly string _graphApiVersion;

    public MessengerChannelProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _graphBaseUrl = (configuration["Meta:GraphBaseUrl"] ?? "https://graph.facebook.com").TrimEnd('/');
        _graphApiVersion = configuration["Meta:GraphApiVersion"] ?? "v21.0";
    }

    public async Task<string> SendMessageAsync(
        string externalCustomerId,
        string? text,
        string accessToken,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_graphBaseUrl}/{_graphApiVersion}/me/messages")
        {
            Content = JsonContent.Create(new
            {
                recipient = new { id = externalCustomerId },
                messaging_type = "RESPONSE",
                message = new { text = text ?? string.Empty }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(content);
        return json.RootElement.TryGetProperty("message_id", out var id)
            ? id.GetString() ?? string.Empty
            : throw new InvalidOperationException("Meta response did not include message_id.");
    }
}
```

### `src/Pasukhi.Infrastructure/Channels/WhatsAppChannelProvider.cs`

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Channels;

public class WhatsAppChannelProvider : IWhatsAppChannelProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _graphBaseUrl;
    private readonly string _graphApiVersion;

    public WhatsAppChannelProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _graphBaseUrl = (configuration["Meta:GraphBaseUrl"] ?? "https://graph.facebook.com").TrimEnd('/');
        _graphApiVersion = configuration["Meta:GraphApiVersion"] ?? "v21.0";
    }

    public async Task<string> SendMessageAsync(
        string externalCustomerId,
        string? text,
        string accessToken,
        string phoneNumberId,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_graphBaseUrl}/{_graphApiVersion}/{phoneNumberId}/messages")
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = externalCustomerId,
                type = "text",
                text = new { body = text ?? string.Empty }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(content);
        if (json.RootElement.TryGetProperty("messages", out var messages) &&
            messages.ValueKind == JsonValueKind.Array &&
            messages.GetArrayLength() > 0 &&
            messages[0].TryGetProperty("id", out var id))
        {
            return id.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException("Meta response did not include a WhatsApp message id.");
    }
}
```

---

## Step 4 — OutboundMessageConsumer

### `src/Pasukhi.Infrastructure/Consumers/OutboundMessageConsumer.cs`

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Consumers;

public class OutboundMessageConsumer : IConsumer<OutboundMessageReadyEvent>
{
    private readonly PasukhiDbContext _db;
    private readonly IInstagramChannelProvider _instagram;
    private readonly IMessengerChannelProvider _messenger;
    private readonly IWhatsAppChannelProvider _whatsApp;
    private readonly ILogger<OutboundMessageConsumer> _logger;

    public OutboundMessageConsumer(
        PasukhiDbContext db,
        IInstagramChannelProvider instagram,
        IMessengerChannelProvider messenger,
        IWhatsAppChannelProvider whatsApp,
        ILogger<OutboundMessageConsumer> logger)
    {
        _db = db;
        _instagram = instagram;
        _messenger = messenger;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OutboundMessageReadyEvent> context)
    {
        var evt = context.Message;
        var ct = context.CancellationToken;

        var message = await _db.Messages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == evt.MessageId && m.BusinessId == evt.BusinessId, ct);

        if (message is null)
        {
            _logger.LogWarning("Outbound message {MessageId} was not found.", evt.MessageId);
            return;
        }

        var channel = await _db.ChannelConnections
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.Id == evt.ChannelConnectionId &&
                c.BusinessId == evt.BusinessId &&
                c.IsActive, ct);

        if (channel is null)
        {
            _logger.LogWarning("Outbound channel {ChannelConnectionId} was not found or inactive.", evt.ChannelConnectionId);
            message.DeliveryStatus = DeliveryStatus.Failed;
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (!Enum.TryParse<ChannelType>(evt.ChannelType, ignoreCase: true, out var channelType))
        {
            _logger.LogWarning("Outbound message {MessageId} has unknown channel type {ChannelType}.", evt.MessageId, evt.ChannelType);
            message.DeliveryStatus = DeliveryStatus.Failed;
            await _db.SaveChangesAsync(ct);
            return;
        }

        try
        {
            var externalMessageId = await SendAsync(channelType, evt, channel.AccessToken, channel.ExternalAccountId, ct);

            message.ExternalMessageId = externalMessageId;
            message.DeliveryStatus = DeliveryStatus.Sent;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Outbound message sent. Message={MessageId} ExternalMessageId={ExternalMessageId}",
                message.Id,
                externalMessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbound message {MessageId} failed to send.", message.Id);
            message.DeliveryStatus = DeliveryStatus.Failed;
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    private Task<string> SendAsync(
        ChannelType channelType,
        OutboundMessageReadyEvent evt,
        string accessToken,
        string externalAccountId,
        CancellationToken ct) =>
        channelType switch
        {
            ChannelType.Instagram => _instagram.SendMessageAsync(evt.ExternalCustomerId, evt.TextContent, accessToken, ct),
            ChannelType.Messenger => _messenger.SendMessageAsync(evt.ExternalCustomerId, evt.TextContent, accessToken, ct),
            ChannelType.WhatsApp => _whatsApp.SendMessageAsync(evt.ExternalCustomerId, evt.TextContent, accessToken, externalAccountId, ct),
            _ => throw new InvalidOperationException($"Unsupported channel type {channelType}.")
        };
}
```

---

## Step 5 — Register Services in Program.cs

Add inside the service registration section:

```csharp
// Channel providers
builder.Services.AddHttpClient<IInstagramChannelProvider, InstagramChannelProvider>();
builder.Services.AddHttpClient<IMessengerChannelProvider, MessengerChannelProvider>();
builder.Services.AddHttpClient<IWhatsAppChannelProvider, WhatsAppChannelProvider>();
```

Register the outbound consumer inside the existing `AddMassTransit` call:

```csharp
x.AddConsumer<OutboundMessageConsumer>();
```

---

## Verification

```bash
dotnet build
```

Trigger an inbound webhook (Phase 3 test). After `InboundMessageConsumer` logs `InboundMessage persisted`, the outbound consumer will fire when an `OutboundMessageReadyEvent` is published (that wiring happens in Phase 7). At this stage, just confirm the build is clean and the consumers are registered.

---

## Commit

```bash
git add src/ docs/codex/phase-5.md
git commit -m "feat(05): outbound message pipeline and channel providers"
```

---

## What's Next

Phase 6: `docs/codex/phase-6.md` — Conversations API (list, detail, send reply) and Escalations (list, detail, resolve).
