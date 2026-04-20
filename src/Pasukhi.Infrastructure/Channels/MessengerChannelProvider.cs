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
        _graphApiVersion = configuration["Meta:GraphApiVersion"] ?? "v20.0";
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
