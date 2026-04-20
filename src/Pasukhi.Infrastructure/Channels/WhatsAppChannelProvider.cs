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
        _graphApiVersion = configuration["Meta:GraphApiVersion"] ?? "v20.0";
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
