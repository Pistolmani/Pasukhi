using Pasukhi.Application.DTOs.Channels;

namespace Pasukhi.Application.Interfaces;

public interface IChannelService
{
    Task<List<ChannelConnectionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ChannelConnectionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ChannelConnectionDto> CreateAsync(CreateChannelConnectionRequest request, CancellationToken cancellationToken = default);
    Task<ChannelConnectionDto> UpdateAsync(Guid id, UpdateChannelConnectionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
