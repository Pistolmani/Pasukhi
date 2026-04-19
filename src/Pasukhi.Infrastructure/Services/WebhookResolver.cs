using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class WebhookResolver : IWebhookResolver
{
    private readonly PasukhiDbContext _db;

    public WebhookResolver(PasukhiDbContext db)
    {
        _db = db;
    }

    public async Task<ChannelConnection?> ResolveAsync(string externalAccountId, ChannelType channelType, CancellationToken ct = default)
    {
        return await _db.ChannelConnections
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.ExternalAccountId == externalAccountId &&
                c.ChannelType == channelType &&
                c.IsActive,
                ct);
    }
}
