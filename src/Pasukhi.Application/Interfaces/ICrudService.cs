namespace Pasukhi.Application.Interfaces;

public interface ICrudService<TDto, TCreate, TUpdate>
{
    Task<List<TDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TDto> CreateAsync(TCreate request, CancellationToken cancellationToken = default);
    Task<TDto> UpdateAsync(Guid id, TUpdate request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
