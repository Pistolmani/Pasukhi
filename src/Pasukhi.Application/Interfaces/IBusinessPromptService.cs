using Pasukhi.Application.DTOs.Ai;

namespace Pasukhi.Application.Interfaces;

public interface IBusinessPromptService
{
    Task<BusinessPromptDto?> GetAsync(CancellationToken ct = default);
    Task<BusinessPromptDto> UpsertAsync(UpsertBusinessPromptRequest request, CancellationToken ct = default);
}
