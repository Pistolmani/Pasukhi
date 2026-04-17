using Mapster;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Faqs;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class FaqService : IFaqService
{
    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public FaqService(PasukhiDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task<List<FaqItemDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.FaqItems
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Question)
            .ProjectToType<FaqItemDto>()
            .ToListAsync(cancellationToken);

    public async Task<FaqItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.FaqItems
            .Where(f => f.Id == id)
            .ProjectToType<FaqItemDto>()
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FaqItemDto> CreateAsync(CreateFaqItemRequest request, CancellationToken cancellationToken = default)
    {
        var businessId = EnsureTenant();

        var faq = new FaqItem
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Question = request.Question.Trim(),
            Answer = request.Answer.Trim(),
            Keywords = NormalizeOptional(request.Keywords),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };

        _db.FaqItems.Add(faq);
        await _db.SaveChangesAsync(cancellationToken);
        return faq.Adapt<FaqItemDto>();
    }

    public async Task<FaqItemDto> UpdateAsync(
        Guid id,
        UpdateFaqItemRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = EnsureTenant();

        var faq = await _db.FaqItems.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"FAQ item {id} not found.");

        faq.Question = request.Question.Trim();
        faq.Answer = request.Answer.Trim();
        faq.Keywords = NormalizeOptional(request.Keywords);
        faq.IsActive = request.IsActive;
        faq.SortOrder = request.SortOrder;

        await _db.SaveChangesAsync(cancellationToken);
        return faq.Adapt<FaqItemDto>();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = EnsureTenant();

        var faq = await _db.FaqItems.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"FAQ item {id} not found.");

        _db.FaqItems.Remove(faq);
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
}
