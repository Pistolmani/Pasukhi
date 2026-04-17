namespace Pasukhi.Application.DTOs.Businesses;

public record BusinessDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? LogoUrl,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateBusinessRequest(
    string Name,
    string Slug,
    string? Description,
    string? LogoUrl
);

public record UpdateBusinessRequest(
    string Name,
    string? Description,
    string? LogoUrl,
    bool IsActive
);
