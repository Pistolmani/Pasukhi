namespace Pasukhi.Application.DTOs.Auth;

public record AuthResponse(string AccessToken, AdminUserDto User);
