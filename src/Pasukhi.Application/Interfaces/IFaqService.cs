using Pasukhi.Application.DTOs.Faqs;

namespace Pasukhi.Application.Interfaces;

public interface IFaqService
{
    Task<List<FaqItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FaqItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FaqItemDto> CreateAsync(CreateFaqItemRequest request, CancellationToken cancellationToken = default);
    Task<FaqItemDto> UpdateAsync(Guid id, UpdateFaqItemRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
