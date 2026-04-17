using Mapster;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Businesses;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class BusinessService : IBusinessService
{
    private readonly PasukhiDbContext _db;

    public BusinessService(PasukhiDbContext db)
    {
        _db = db;
    }

    public async Task<List<BusinessDto>> GetAllAsync() =>
        await _db.Businesses
            .OrderBy(b => b.Name)
            .ProjectToType<BusinessDto>()
            .ToListAsync();

    public async Task<BusinessDto?> GetByIdAsync(Guid id) =>
        await _db.Businesses
            .Where(b => b.Id == id)
            .ProjectToType<BusinessDto>()
            .FirstOrDefaultAsync();

    public async Task<BusinessDto> CreateAsync(CreateBusinessRequest request)
    {
        if (await _db.Businesses.AnyAsync(b => b.Slug == request.Slug))
        {
            throw new InvalidOperationException($"Slug '{request.Slug}' is already taken.");
        }

        var business = new Business
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug,
            Description = request.Description,
            LogoUrl = request.LogoUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Businesses.Add(business);
        await _db.SaveChangesAsync();
        return business.Adapt<BusinessDto>();
    }

    public async Task<BusinessDto> UpdateAsync(Guid id, UpdateBusinessRequest request)
    {
        var business = await _db.Businesses.FindAsync(id)
            ?? throw new KeyNotFoundException($"Business {id} not found.");

        business.Name = request.Name;
        business.Description = request.Description;
        business.LogoUrl = request.LogoUrl;
        business.IsActive = request.IsActive;
        business.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return business.Adapt<BusinessDto>();
    }

    public async Task DeleteAsync(Guid id)
    {
        var business = await _db.Businesses.FindAsync(id)
            ?? throw new KeyNotFoundException($"Business {id} not found.");

        _db.Businesses.Remove(business);
        await _db.SaveChangesAsync();
    }
}
