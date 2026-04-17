using Pasukhi.Application.DTOs.Businesses;

namespace Pasukhi.Application.Interfaces;

public interface IBusinessService
{
    Task<List<BusinessDto>> GetAllAsync();
    Task<BusinessDto?> GetByIdAsync(Guid id);
    Task<BusinessDto> CreateAsync(CreateBusinessRequest request);
    Task<BusinessDto> UpdateAsync(Guid id, UpdateBusinessRequest request);
    Task DeleteAsync(Guid id);
}
