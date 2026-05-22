using System.Security.Cryptography;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Channels;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class ChannelService : IChannelService
{
    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPlanLimitsService _planLimits;

    public ChannelService(PasukhiDbContext db, ITenantProvider tenantProvider, IPlanLimitsService planLimits)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _planLimits = planLimits;
    }

    public async Task<List<ChannelConnectionDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.ChannelConnections
            .OrderBy(c => c.ChannelType)
            .ThenBy(c => c.ExternalAccountName)
            .ProjectToType<ChannelConnectionDto>()
            .ToListAsync(cancellationToken);

    public async Task<ChannelConnectionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.ChannelConnections
            .Where(c => c.Id == id)
            .ProjectToType<ChannelConnectionDto>()
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ChannelConnectionDto> CreateAsync(
        CreateChannelConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var businessId = EnsureTenant();
        await _planLimits.EnsureCanAddChannelAsync(cancellationToken);
        var externalAccountId = request.ExternalAccountId.Trim();

        var exists = await _db.ChannelConnections
            .IgnoreQueryFilters()
            .AnyAsync(
                c => c.ChannelType == request.ChannelType && c.ExternalAccountId == externalAccountId,
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("A channel connection already exists for this external account.");
        }

        var channel = new ChannelConnection
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            ChannelType = request.ChannelType,
            ExternalAccountId = externalAccountId,
            ExternalAccountName = NormalizeOptional(request.ExternalAccountName),
            AccessToken = request.AccessToken.Trim(),
            VerifyToken = string.IsNullOrWhiteSpace(request.VerifyToken)
                ? GenerateVerifyToken()
                : request.VerifyToken.Trim(),
            IsActive = request.IsActive
        };

        _db.ChannelConnections.Add(channel);
        await _db.SaveChangesAsync(cancellationToken);
        return channel.Adapt<ChannelConnectionDto>();
    }

    public async Task<ChannelConnectionDto> UpdateAsync(
        Guid id,
        UpdateChannelConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = EnsureTenant();

        var channel = await _db.ChannelConnections.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Channel connection {id} not found.");

        channel.ExternalAccountId = request.ExternalAccountId.Trim();
        channel.ExternalAccountName = NormalizeOptional(request.ExternalAccountName);
        channel.AccessToken = request.AccessToken.Trim();
        channel.VerifyToken = request.VerifyToken.Trim();
        channel.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return channel.Adapt<ChannelConnectionDto>();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = EnsureTenant();

        var channel = await _db.ChannelConnections.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Channel connection {id} not found.");

        _db.ChannelConnections.Remove(channel);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private Guid EnsureTenant()
    {
        if (_tenantProvider.BusinessId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        return _tenantProvider.BusinessId;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GenerateVerifyToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}
