namespace Pasukhi.Application.DTOs.Auth;

public record AuthResponse(string AccessToken, string RawRefreshToken, AdminUserDto User);
