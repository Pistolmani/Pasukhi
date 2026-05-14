using Pasukhi.Application.DTOs.Auth;

namespace Pasukhi.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> ExternalLoginAsync(string provider, string providerId, string email, string firstName, string lastName);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(string userId);
    Task<AdminUserDto> GetCurrentUserAsync(string userId);
}
