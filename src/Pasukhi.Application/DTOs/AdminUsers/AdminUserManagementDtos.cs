namespace Pasukhi.Application.DTOs.AdminUsers;

public record ManagedAdminUserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    Guid? BusinessId,
    string? BusinessName,
    DateTime CreatedAt);

public record CreateAdminUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    Guid BusinessId);
