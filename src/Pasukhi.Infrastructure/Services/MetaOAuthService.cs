using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Services;

public class MetaOAuthService : IMetaOAuthService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public MetaOAuthService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<string> ExchangeCodeForTokenAsync(string code, string redirectUri)
    {
        var graphBase = _config["Meta:GraphBaseUrl"] ?? "https://graph.facebook.com";
        var version = _config["Meta:GraphApiVersion"] ?? "v21.0";

        var url = $"{graphBase}/{version}/oauth/access_token" +
            $"?client_id={Uri.EscapeDataString(_config["Meta:AppId"]!)}" +
            $"&client_secret={Uri.EscapeDataString(_config["Meta:AppSecret"]!)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&code={Uri.EscapeDataString(code)}";

        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return result?.AccessToken ?? throw new InvalidOperationException("No access token in Meta response");
    }

    public async Task<MetaUserProfile> GetUserProfileAsync(string accessToken)
    {
        var graphBase = _config["Meta:GraphBaseUrl"] ?? "https://graph.facebook.com";

        var url = $"{graphBase}/me?fields=id,first_name,last_name,email&access_token={Uri.EscapeDataString(accessToken)}";

        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>()
            ?? throw new InvalidOperationException("No profile data in Meta response");

        return new MetaUserProfile(
            profile.Id,
            profile.Email ?? "",
            profile.FirstName ?? "",
            profile.LastName ?? "");
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
    }

    private sealed class ProfileResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }
    }
}
