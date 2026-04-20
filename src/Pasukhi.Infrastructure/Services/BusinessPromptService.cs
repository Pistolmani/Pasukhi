using Mapster;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Ai;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class BusinessPromptService : IBusinessPromptService
{
    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public BusinessPromptService(PasukhiDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task<BusinessPromptDto?> GetAsync(CancellationToken ct = default) =>
        await _db.BusinessPrompts
            .ProjectToType<BusinessPromptDto>()
            .FirstOrDefaultAsync(ct);

    public async Task<BusinessPromptDto> UpsertAsync(UpsertBusinessPromptRequest request, CancellationToken ct = default)
    {
        var businessId = EnsureTenant();

        var prompt = await _db.BusinessPrompts.FirstOrDefaultAsync(ct);

        if (prompt is null)
        {
            prompt = new BusinessPrompt
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId
            };
            _db.BusinessPrompts.Add(prompt);
        }

        prompt.IsAiEnabled = request.IsAiEnabled;
        prompt.SystemPrompt = request.SystemPrompt.Trim();
        prompt.ToneDescription = request.ToneDescription.Trim();
        prompt.EscalationMessage = request.EscalationMessage.Trim();
        prompt.MaxAiTokensPerDay = Math.Max(0, request.MaxAiTokensPerDay);
        prompt.AiConfidenceThreshold = Math.Clamp(request.AiConfidenceThreshold, 0, 1);
        prompt.FaqConfidenceThreshold = Math.Clamp(request.FaqConfidenceThreshold, 0, 1);

        await _db.SaveChangesAsync(ct);
        return prompt.Adapt<BusinessPromptDto>();
    }

    private Guid EnsureTenant()
    {
        if (_tenantProvider.BusinessId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required.");
        return _tenantProvider.BusinessId;
    }
}
