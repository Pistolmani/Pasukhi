using Mapster;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Rules;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class RuleService : IRuleService
{
    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public RuleService(PasukhiDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task<List<AutomationRuleDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.AutomationRules
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Name)
            .ProjectToType<AutomationRuleDto>()
            .ToListAsync(cancellationToken);

    public async Task<AutomationRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.AutomationRules
            .Where(r => r.Id == id)
            .ProjectToType<AutomationRuleDto>()
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<AutomationRuleDto> CreateAsync(
        CreateAutomationRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var businessId = EnsureTenant();

        var rule = new AutomationRule
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Priority = request.Priority,
            TriggerType = request.TriggerType,
            TriggerValue = request.TriggerValue.Trim(),
            ActionType = request.ActionType,
            ActionValue = request.ActionValue.Trim(),
            IsActive = request.IsActive
        };

        _db.AutomationRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);
        return rule.Adapt<AutomationRuleDto>();
    }

    public async Task<AutomationRuleDto> UpdateAsync(
        Guid id,
        UpdateAutomationRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = EnsureTenant();

        var rule = await _db.AutomationRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Automation rule {id} not found.");

        rule.Name = request.Name.Trim();
        rule.Priority = request.Priority;
        rule.TriggerType = request.TriggerType;
        rule.TriggerValue = request.TriggerValue.Trim();
        rule.ActionType = request.ActionType;
        rule.ActionValue = request.ActionValue.Trim();
        rule.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return rule.Adapt<AutomationRuleDto>();
    }

    public async Task UpdatePrioritiesAsync(UpdateRulePrioritiesRequest request, CancellationToken cancellationToken = default)
    {
        _ = EnsureTenant();

        var requestedIds = request.Items.Select(i => i.Id).Distinct().ToList();
        var rules = await _db.AutomationRules
            .Where(r => requestedIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        if (rules.Count != requestedIds.Count)
        {
            throw new KeyNotFoundException("One or more automation rules were not found.");
        }

        foreach (var item in request.Items)
        {
            var rule = rules.First(r => r.Id == item.Id);
            rule.Priority = item.Priority;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = EnsureTenant();

        var rule = await _db.AutomationRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Automation rule {id} not found.");

        _db.AutomationRules.Remove(rule);
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
}
