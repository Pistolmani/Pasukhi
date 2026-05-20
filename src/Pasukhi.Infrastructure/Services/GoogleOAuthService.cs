using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Services;

public class GoogleOAuthService : IGoogleOAuthService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public GoogleOAuthService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<string> ExchangeCodeForTokenAsync(string code, string redirectUri)
    {
        var response = await _http.PostAsJsonAsync("https://oauth2.googleapis.com/token", new
        {
            code,
            client_id = _config["Google:ClientId"],
            client_secret = _config["Google:ClientSecret"],
            redirect_uri = redirectUri,
            grant_type = "authorization_code",
        });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return result?.AccessToken ?? throw new InvalidOperationException("No access token in Google response");
    }

    public async Task<GoogleUserProfile> GetUserProfileAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>()
            ?? throw new InvalidOperationException("No profile data in Google response");

        return new GoogleUserProfile(
            profile.Id,
            profile.Email ?? "",
            profile.GivenName ?? "",
            profile.FamilyName ?? "");
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

        [JsonPropertyName("given_name")]
        public string? GivenName { get; set; }

        [JsonPropertyName("family_name")]
        public string? FamilyName { get; set; }
    }
}
