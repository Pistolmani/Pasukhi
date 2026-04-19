using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Interfaces;

public interface IWebhookResolver
{
    Task<ChannelConnection?> ResolveAsync(string externalAccountId, ChannelType channelType, CancellationToken ct = default);
}
