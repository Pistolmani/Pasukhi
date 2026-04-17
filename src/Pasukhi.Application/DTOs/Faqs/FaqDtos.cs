namespace Pasukhi.Application.DTOs.Faqs;

public record FaqItemDto(
    Guid Id,
    Guid BusinessId,
    string Question,
    string Answer,
    string? Keywords,
    int MatchCount,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateFaqItemRequest(
    string Question,
    string Answer,
    string? Keywords,
    bool IsActive,
    int SortOrder);

public record UpdateFaqItemRequest(
    string Question,
    string Answer,
    string? Keywords,
    bool IsActive,
    int SortOrder);
