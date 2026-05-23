using Pasukhi.Application.DTOs.Channels;

namespace Pasukhi.Application.Interfaces;

public interface IChannelService : ICrudService<ChannelConnectionDto, CreateChannelConnectionRequest, UpdateChannelConnectionRequest>
{
}
