namespace Pasukhi.Application.Interfaces;

public record GoogleUserProfile(string Id, string Email, string FirstName, string LastName);

public interface IGoogleOAuthService
{
    Task<string> ExchangeCodeForTokenAsync(string code, string redirectUri);
    Task<GoogleUserProfile> GetUserProfileAsync(string accessToken);
}
