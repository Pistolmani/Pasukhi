using Pasukhi.Application.DTOs.Escalations;

namespace Pasukhi.Application.Interfaces;

public interface IEscalationService
{
    Task<List<EscalationListItemDto>> GetAllAsync(bool includeResolved = false, CancellationToken ct = default);
    Task<EscalationDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task ResolveAsync(Guid id, ResolveEscalationRequest request, CancellationToken ct = default);
}
