namespace Pasukhi.Application.Interfaces;

public record MetaUserProfile(string Id, string Email, string FirstName, string LastName);

public interface IMetaOAuthService
{
    Task<string> ExchangeCodeForTokenAsync(string code, string redirectUri);
    Task<MetaUserProfile> GetUserProfileAsync(string accessToken);
}
