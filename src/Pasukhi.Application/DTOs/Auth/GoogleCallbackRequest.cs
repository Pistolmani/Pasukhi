namespace Pasukhi.Application.DTOs.Auth;

public record GoogleCallbackRequest(string Code, string RedirectUri);
