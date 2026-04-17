namespace Pasukhi.Application.DTOs.Auth;

public record AdminUserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    Guid? BusinessId
);
