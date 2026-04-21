using Pasukhi.Application.DTOs.Settings;

namespace Pasukhi.Application.Interfaces;

public interface ISettingsService
{
    Task<BusinessSettingsDto> GetAsync(CancellationToken ct = default);
    Task<BusinessSettingsDto> UpdateAsync(UpdateBusinessSettingsRequest request, CancellationToken ct = default);
}
