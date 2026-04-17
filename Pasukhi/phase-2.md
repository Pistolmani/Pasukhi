# Codex Task - Phase 2: Channels, FAQs, Rules, and Matchers

> Read `AGENTS.md` first. Phase 0 and Phase 1 must be complete before starting this.

## Goal

By the end of this phase:
- Operators can manage channel connections for Instagram, Messenger, and WhatsApp
- Operators can manage FAQ items for the active business
- Operators can manage automation rules for the active business
- The backend has deterministic `IFaqMatcher` and `IRuleMatcher` services
- The frontend has authenticated admin pages for Channels, FAQs, and Rules
- Nothing is wired into webhooks yet; that begins in Phase 3

---

## Repo root

`C:\Users\piros\OneDrive\Desktop\Pasukhi\`

---

## Step 1 - Backend DTOs

Create these DTO files in the Application project.

### `src/Pasukhi.Application/DTOs/Channels/ChannelDtos.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.DTOs.Channels;

public record ChannelConnectionDto(
    Guid Id,
    Guid BusinessId,
    ChannelType ChannelType,
    string ExternalAccountId,
    string? ExternalAccountName,
    string AccessToken,
    string VerifyToken,
    bool IsActive,
    DateTime? LastWebhookAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateChannelConnectionRequest(
    ChannelType ChannelType,
    string ExternalAccountId,
    string? ExternalAccountName,
    string AccessToken,
    string? VerifyToken,
    bool IsActive);

public record UpdateChannelConnectionRequest(
    string ExternalAccountId,
    string? ExternalAccountName,
    string AccessToken,
    string VerifyToken,
    bool IsActive);
```

### `src/Pasukhi.Application/DTOs/Faqs/FaqDtos.cs`

```csharp
namespace Pasukhi.Application.DTOs.Faqs;

public record FaqItemDto(
    Guid Id,
    Guid BusinessId,
    string Question,
    string Answer,
    string? Keywords,
    int MatchCount,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateFaqItemRequest(
    string Question,
    string Answer,
    string? Keywords,
    bool IsActive,
    int SortOrder);

public record UpdateFaqItemRequest(
    string Question,
    string Answer,
    string? Keywords,
    bool IsActive,
    int SortOrder);
```

### `src/Pasukhi.Application/DTOs/Rules/RuleDtos.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.DTOs.Rules;

public record AutomationRuleDto(
    Guid Id,
    Guid BusinessId,
    string Name,
    int Priority,
    TriggerType TriggerType,
    string TriggerValue,
    ActionType ActionType,
    string ActionValue,
    bool IsActive,
    int MatchCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateAutomationRuleRequest(
    string Name,
    int Priority,
    TriggerType TriggerType,
    string TriggerValue,
    ActionType ActionType,
    string ActionValue,
    bool IsActive);

public record UpdateAutomationRuleRequest(
    string Name,
    int Priority,
    TriggerType TriggerType,
    string TriggerValue,
    ActionType ActionType,
    string ActionValue,
    bool IsActive);

public record RulePriorityItem(Guid Id, int Priority);

public record UpdateRulePrioritiesRequest(IReadOnlyList<RulePriorityItem> Items);
```

---

## Step 2 - Service and Matcher Contracts

Create these interfaces in the Application project.

### `src/Pasukhi.Application/Interfaces/IChannelService.cs`

```csharp
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
```

### `src/Pasukhi.Application/Interfaces/IFaqService.cs`

```csharp
using Pasukhi.Application.DTOs.Faqs;

namespace Pasukhi.Application.Interfaces;

public interface IFaqService
{
    Task<List<FaqItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FaqItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FaqItemDto> CreateAsync(CreateFaqItemRequest request, CancellationToken cancellationToken = default);
    Task<FaqItemDto> UpdateAsync(Guid id, UpdateFaqItemRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
```

### `src/Pasukhi.Application/Interfaces/IRuleService.cs`

```csharp
using Pasukhi.Application.DTOs.Rules;

namespace Pasukhi.Application.Interfaces;

public interface IRuleService
{
    Task<List<AutomationRuleDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AutomationRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AutomationRuleDto> CreateAsync(CreateAutomationRuleRequest request, CancellationToken cancellationToken = default);
    Task<AutomationRuleDto> UpdateAsync(Guid id, UpdateAutomationRuleRequest request, CancellationToken cancellationToken = default);
    Task UpdatePrioritiesAsync(UpdateRulePrioritiesRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
```

### `src/Pasukhi.Application/Interfaces/IFaqMatcher.cs`

```csharp
using Pasukhi.Domain.Entities;

namespace Pasukhi.Application.Interfaces;

public record FaqMatchResult(FaqItem FaqItem, double Confidence);

public interface IFaqMatcher
{
    Task<FaqMatchResult?> FindBestMatchAsync(
        Guid businessId,
        string messageText,
        CancellationToken cancellationToken = default);
}
```

### `src/Pasukhi.Application/Interfaces/IRuleMatcher.cs`

```csharp
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.Interfaces;

public record RuleMatchResult(AutomationRule Rule);

public interface IRuleMatcher
{
    Task<IReadOnlyList<RuleMatchResult>> FindMatchesAsync(
        Guid businessId,
        string messageText,
        MessageType messageType,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default);
}
```

---

## Step 3 - Validators

Create validators for all new write requests.

### `src/Pasukhi.Application/Validators/CreateChannelConnectionRequestValidator.cs`

```csharp
using FluentValidation;
using Pasukhi.Application.DTOs.Channels;

namespace Pasukhi.Application.Validators;

public class CreateChannelConnectionRequestValidator : AbstractValidator<CreateChannelConnectionRequest>
{
    public CreateChannelConnectionRequestValidator()
    {
        RuleFor(x => x.ExternalAccountId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExternalAccountName).MaximumLength(200);
        RuleFor(x => x.AccessToken).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.VerifyToken).MaximumLength(200);
    }
}
```

### `src/Pasukhi.Application/Validators/UpdateChannelConnectionRequestValidator.cs`

```csharp
using FluentValidation;
using Pasukhi.Application.DTOs.Channels;

namespace Pasukhi.Application.Validators;

public class UpdateChannelConnectionRequestValidator : AbstractValidator<UpdateChannelConnectionRequest>
{
    public UpdateChannelConnectionRequestValidator()
    {
        RuleFor(x => x.ExternalAccountId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExternalAccountName).MaximumLength(200);
        RuleFor(x => x.AccessToken).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.VerifyToken).NotEmpty().MaximumLength(200);
    }
}
```

### `src/Pasukhi.Application/Validators/CreateFaqItemRequestValidator.cs`

```csharp
using FluentValidation;
using Pasukhi.Application.DTOs.Faqs;

namespace Pasukhi.Application.Validators;

public class CreateFaqItemRequestValidator : AbstractValidator<CreateFaqItemRequest>
{
    public CreateFaqItemRequestValidator()
    {
        RuleFor(x => x.Question).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Keywords).MaximumLength(1000);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
```

### `src/Pasukhi.Application/Validators/UpdateFaqItemRequestValidator.cs`

```csharp
using FluentValidation;
using Pasukhi.Application.DTOs.Faqs;

namespace Pasukhi.Application.Validators;

public class UpdateFaqItemRequestValidator : AbstractValidator<UpdateFaqItemRequest>
{
    public UpdateFaqItemRequestValidator()
    {
        RuleFor(x => x.Question).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Keywords).MaximumLength(1000);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
```

### `src/Pasukhi.Application/Validators/CreateAutomationRuleRequestValidator.cs`

```csharp
using FluentValidation;
using Pasukhi.Application.DTOs.Rules;

namespace Pasukhi.Application.Validators;

public class CreateAutomationRuleRequestValidator : AbstractValidator<CreateAutomationRuleRequest>
{
    public CreateAutomationRuleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TriggerValue).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.ActionValue).NotEmpty().MaximumLength(4000);
    }
}
```

### `src/Pasukhi.Application/Validators/UpdateAutomationRuleRequestValidator.cs`

```csharp
using FluentValidation;
using Pasukhi.Application.DTOs.Rules;

namespace Pasukhi.Application.Validators;

public class UpdateAutomationRuleRequestValidator : AbstractValidator<UpdateAutomationRuleRequest>
{
    public UpdateAutomationRuleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TriggerValue).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.ActionValue).NotEmpty().MaximumLength(4000);
    }
}
```

### `src/Pasukhi.Application/Validators/UpdateRulePrioritiesRequestValidator.cs`

```csharp
using FluentValidation;
using Pasukhi.Application.DTOs.Rules;

namespace Pasukhi.Application.Validators;

public class UpdateRulePrioritiesRequestValidator : AbstractValidator<UpdateRulePrioritiesRequest>
{
    public UpdateRulePrioritiesRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Id).NotEmpty();
            item.RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        });
    }
}
```

---

## Step 4 - CRUD Services

Create these service implementations in the Infrastructure project.

### `src/Pasukhi.Infrastructure/Services/ChannelService.cs`

```csharp
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

    public ChannelService(PasukhiDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
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

        var exists = await _db.ChannelConnections.AnyAsync(
            c => c.ChannelType == request.ChannelType && c.ExternalAccountId == request.ExternalAccountId,
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
            ExternalAccountId = request.ExternalAccountId.Trim(),
            ExternalAccountName = request.ExternalAccountName?.Trim(),
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
        channel.ExternalAccountName = request.ExternalAccountName?.Trim();
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

    private static string GenerateVerifyToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}
```

### `src/Pasukhi.Infrastructure/Services/FaqService.cs`

```csharp
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
```

### `src/Pasukhi.Infrastructure/Services/RuleService.cs`

```csharp
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
```

---

## Step 5 - Matcher Implementations

These matchers are deterministic and intentionally simple. They are good enough to wire into the message pipeline later, and they keep AI out of Phase 2.

### `src/Pasukhi.Infrastructure/Services/FaqMatcher.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class FaqMatcher : IFaqMatcher
{
    private const double DefaultThreshold = 0.85;
    private readonly PasukhiDbContext _db;

    public FaqMatcher(PasukhiDbContext db)
    {
        _db = db;
    }

    public async Task<FaqMatchResult?> FindBestMatchAsync(
        Guid businessId,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        if (businessId == Guid.Empty || string.IsNullOrWhiteSpace(messageText))
        {
            return null;
        }

        var threshold = await _db.BusinessPrompts
            .IgnoreQueryFilters()
            .Where(p => p.BusinessId == businessId)
            .Select(p => (double?)p.FaqConfidenceThreshold)
            .FirstOrDefaultAsync(cancellationToken) ?? DefaultThreshold;

        var faqs = await _db.FaqItems
            .IgnoreQueryFilters()
            .Where(f => f.BusinessId == businessId && f.IsActive)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Question)
            .ToListAsync(cancellationToken);

        var normalizedMessage = Normalize(messageText);

        var best = faqs
            .Select(faq => new
            {
                Faq = faq,
                Confidence = ScoreFaq(faq, normalizedMessage)
            })
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.Faq.SortOrder)
            .FirstOrDefault();

        if (best == null || best.Confidence < threshold)
        {
            return null;
        }

        best.Faq.MatchCount++;
        await _db.SaveChangesAsync(cancellationToken);

        return new FaqMatchResult(best.Faq, best.Confidence);
    }

    private static double ScoreFaq(FaqItem faq, string normalizedMessage)
    {
        var normalizedQuestion = Normalize(faq.Question);

        if (normalizedMessage == normalizedQuestion)
        {
            return 1.0;
        }

        if (normalizedMessage.Contains(normalizedQuestion) || normalizedQuestion.Contains(normalizedMessage))
        {
            return 0.92;
        }

        var keywordScore = ScoreKeywords(faq.Keywords, normalizedMessage);
        var tokenOverlapScore = ScoreTokenOverlap(normalizedMessage, normalizedQuestion);

        return Math.Max(keywordScore, tokenOverlapScore);
    }

    private static double ScoreKeywords(string? keywords, string normalizedMessage)
    {
        var parts = SplitConfiguredValues(keywords).Select(Normalize).Where(v => v.Length > 0).ToList();
        if (parts.Count == 0)
        {
            return 0.0;
        }

        var matches = parts.Count(normalizedMessage.Contains);
        if (matches == 0)
        {
            return 0.0;
        }

        var ratio = (double)matches / parts.Count;
        return Math.Min(0.9, 0.65 + (ratio * 0.25));
    }

    private static double ScoreTokenOverlap(string normalizedMessage, string normalizedQuestion)
    {
        var messageTokens = ToTokens(normalizedMessage);
        var questionTokens = ToTokens(normalizedQuestion);

        if (messageTokens.Count == 0 || questionTokens.Count == 0)
        {
            return 0.0;
        }

        var overlap = questionTokens.Count(messageTokens.Contains);
        var ratio = (double)overlap / questionTokens.Count;

        return ratio == 0 ? 0.0 : 0.4 + (ratio * 0.45);
    }

    private static IReadOnlyList<string> SplitConfiguredValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(new[] { ',', ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static HashSet<string> ToTokens(string value) =>
        Normalize(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 1)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Normalize(string value)
    {
        var cleaned = new string(value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ')
            .ToArray());

        return string.Join(" ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
```

### `src/Pasukhi.Infrastructure/Services/RuleMatcher.cs`

```csharp
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class RuleMatcher : IRuleMatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private readonly PasukhiDbContext _db;

    public RuleMatcher(PasukhiDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RuleMatchResult>> FindMatchesAsync(
        Guid businessId,
        string messageText,
        MessageType messageType,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        if (businessId == Guid.Empty)
        {
            return Array.Empty<RuleMatchResult>();
        }

        var rules = await _db.AutomationRules
            .IgnoreQueryFilters()
            .Where(r => r.BusinessId == businessId && r.IsActive)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var matches = new List<RuleMatchResult>();

        foreach (var rule in rules)
        {
            if (!IsMatch(rule, messageText, messageType, receivedAt))
            {
                continue;
            }

            rule.MatchCount++;
            matches.Add(new RuleMatchResult(rule));
        }

        if (matches.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return matches;
    }

    private static bool IsMatch(AutomationRule rule, string messageText, MessageType messageType, DateTimeOffset receivedAt) =>
        rule.TriggerType switch
        {
            TriggerType.Keyword => MatchesKeyword(rule.TriggerValue, messageText),
            TriggerType.Regex => MatchesRegex(rule.TriggerValue, messageText),
            TriggerType.MessageType => MatchesMessageType(rule.TriggerValue, messageType),
            TriggerType.TimeOfDay => MatchesTimeOfDay(rule.TriggerValue, receivedAt),
            _ => false
        };

    private static bool MatchesKeyword(string triggerValue, string messageText)
    {
        var normalizedMessage = Normalize(messageText);
        return SplitConfiguredValues(triggerValue)
            .Select(Normalize)
            .Where(value => value.Length > 0)
            .Any(normalizedMessage.Contains);
    }

    private static bool MatchesRegex(string pattern, string messageText)
    {
        try
        {
            return Regex.IsMatch(
                messageText ?? string.Empty,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                RegexTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool MatchesMessageType(string triggerValue, MessageType messageType)
    {
        if (Enum.TryParse<MessageType>(triggerValue, ignoreCase: true, out var expected))
        {
            return expected == messageType;
        }

        return int.TryParse(triggerValue, out var value) && value == (int)messageType;
    }

    private static bool MatchesTimeOfDay(string triggerValue, DateTimeOffset receivedAt)
    {
        var parts = triggerValue.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !TimeSpan.TryParse(parts[0], out var start) || !TimeSpan.TryParse(parts[1], out var end))
        {
            return false;
        }

        var current = receivedAt.TimeOfDay;
        return start <= end
            ? current >= start && current <= end
            : current >= start || current <= end;
    }

    private static IReadOnlyList<string> SplitConfiguredValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(new[] { ',', ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Normalize(string value)
    {
        var cleaned = new string((value ?? string.Empty)
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ')
            .ToArray());

        return string.Join(" ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
```

---

## Step 6 - API Controllers

Create one authenticated controller for each new admin area.

### `src/Pasukhi.API/Controllers/ChannelsController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Channels;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/channels")]
[Authorize]
public class ChannelsController : ControllerBase
{
    private readonly IChannelService _channels;

    public ChannelsController(IChannelService channels)
    {
        _channels = channels;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _channels.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _channels.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateChannelConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _channels.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateChannelConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _channels.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _channels.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
```

### `src/Pasukhi.API/Controllers/FaqsController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Faqs;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/faqs")]
[Authorize]
public class FaqsController : ControllerBase
{
    private readonly IFaqService _faqs;

    public FaqsController(IFaqService faqs)
    {
        _faqs = faqs;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _faqs.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _faqs.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateFaqItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _faqs.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateFaqItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _faqs.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _faqs.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
```

### `src/Pasukhi.API/Controllers/RulesController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Rules;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/rules")]
[Authorize]
public class RulesController : ControllerBase
{
    private readonly IRuleService _rules;

    public RulesController(IRuleService rules)
    {
        _rules = rules;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _rules.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _rules.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAutomationRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _rules.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAutomationRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _rules.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("priorities")]
    public async Task<IActionResult> UpdatePriorities(
        [FromBody] UpdateRulePrioritiesRequest request,
        CancellationToken cancellationToken)
    {
        await _rules.UpdatePrioritiesAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _rules.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
```

---

## Step 7 - Register Services

Update `src/Pasukhi.API/Program.cs`.

Add these registrations near the existing scoped service registrations:

```csharp
builder.Services.AddScoped<IChannelService, ChannelService>();
builder.Services.AddScoped<IFaqService, FaqService>();
builder.Services.AddScoped<IRuleService, RuleService>();
builder.Services.AddScoped<IFaqMatcher, FaqMatcher>();
builder.Services.AddScoped<IRuleMatcher, RuleMatcher>();
```

No new migration is required in this phase. The Phase 0 migration already created the channel, FAQ, and automation rule tables.

---

## Step 8 - Seed a Local Tenant

The Phase 1 default admin is a `SuperAdmin` without a tenant. These new endpoints are tenant-scoped, so update the seed data to create a demo business and attach the default admin to it.

### `src/Pasukhi.Infrastructure/Data/DbSeeder.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pasukhi.Domain.Entities;

namespace Pasukhi.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PasukhiDbContext>();
        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in new[] { "SuperAdmin", "Operator" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Slug == "demo");
        if (business == null)
        {
            business = new Business
            {
                Id = Guid.NewGuid(),
                Name = "Demo Business",
                Slug = "demo",
                Description = "Local development tenant",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Businesses.Add(business);
            await db.SaveChangesAsync();
        }

        const string adminEmail = "admin@pasukhi.ge";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new AdminUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Super",
                LastName = "Admin",
                BusinessId = business.Id,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            await userManager.CreateAsync(admin, "Admin@123!");
        }
        else if (admin.BusinessId == null)
        {
            admin.BusinessId = business.Id;
            await userManager.UpdateAsync(admin);
        }

        if (!await userManager.IsInRoleAsync(admin, "SuperAdmin"))
        {
            await userManager.AddToRoleAsync(admin, "SuperAdmin");
        }
    }
}
```

---

## Step 9 - Frontend Types and API Clients

Create typed clients for the new endpoints.

### `pasukhi-admin/src/types/channel.ts`

```ts
export type ChannelType = 0 | 1 | 2

export const channelTypeLabels: Record<ChannelType, string> = {
  0: 'Instagram',
  1: 'Messenger',
  2: 'WhatsApp',
}

export interface ChannelConnection {
  id: string
  businessId: string
  channelType: ChannelType
  externalAccountId: string
  externalAccountName: string | null
  accessToken: string
  verifyToken: string
  isActive: boolean
  lastWebhookAt: string | null
  createdAt: string
  updatedAt: string
}

export interface SaveChannelConnectionRequest {
  channelType: ChannelType
  externalAccountId: string
  externalAccountName: string | null
  accessToken: string
  verifyToken?: string | null
  isActive: boolean
}
```

### `pasukhi-admin/src/types/faq.ts`

```ts
export interface FaqItem {
  id: string
  businessId: string
  question: string
  answer: string
  keywords: string | null
  matchCount: number
  isActive: boolean
  sortOrder: number
  createdAt: string
  updatedAt: string
}

export interface SaveFaqItemRequest {
  question: string
  answer: string
  keywords: string | null
  isActive: boolean
  sortOrder: number
}
```

### `pasukhi-admin/src/types/rule.ts`

```ts
export type TriggerType = 0 | 1 | 2 | 3
export type ActionType = 0 | 1 | 2

export const triggerTypeLabels: Record<TriggerType, string> = {
  0: 'Keyword',
  1: 'Regex',
  2: 'Message type',
  3: 'Time of day',
}

export const actionTypeLabels: Record<ActionType, string> = {
  0: 'Send reply',
  1: 'Tag conversation',
  2: 'Escalate',
}

export interface AutomationRule {
  id: string
  businessId: string
  name: string
  priority: number
  triggerType: TriggerType
  triggerValue: string
  actionType: ActionType
  actionValue: string
  isActive: boolean
  matchCount: number
  createdAt: string
  updatedAt: string
}

export interface SaveAutomationRuleRequest {
  name: string
  priority: number
  triggerType: TriggerType
  triggerValue: string
  actionType: ActionType
  actionValue: string
  isActive: boolean
}
```

### `pasukhi-admin/src/api/channels.ts`

```ts
import api from './client'
import type { ChannelConnection, SaveChannelConnectionRequest } from '../types/channel'

export const channelsApi = {
  list: () =>
    api.get<ChannelConnection[]>('/api/channels').then((response) => response.data),
  create: (data: SaveChannelConnectionRequest) =>
    api.post<ChannelConnection>('/api/channels', data).then((response) => response.data),
  update: (id: string, data: SaveChannelConnectionRequest) =>
    api.put<ChannelConnection>(`/api/channels/${id}`, data).then((response) => response.data),
  remove: (id: string) => api.delete(`/api/channels/${id}`),
}
```

### `pasukhi-admin/src/api/faqs.ts`

```ts
import api from './client'
import type { FaqItem, SaveFaqItemRequest } from '../types/faq'

export const faqsApi = {
  list: () =>
    api.get<FaqItem[]>('/api/faqs').then((response) => response.data),
  create: (data: SaveFaqItemRequest) =>
    api.post<FaqItem>('/api/faqs', data).then((response) => response.data),
  update: (id: string, data: SaveFaqItemRequest) =>
    api.put<FaqItem>(`/api/faqs/${id}`, data).then((response) => response.data),
  remove: (id: string) => api.delete(`/api/faqs/${id}`),
}
```

### `pasukhi-admin/src/api/rules.ts`

```ts
import api from './client'
import type { AutomationRule, SaveAutomationRuleRequest } from '../types/rule'

export const rulesApi = {
  list: () =>
    api.get<AutomationRule[]>('/api/rules').then((response) => response.data),
  create: (data: SaveAutomationRuleRequest) =>
    api.post<AutomationRule>('/api/rules', data).then((response) => response.data),
  update: (id: string, data: SaveAutomationRuleRequest) =>
    api.put<AutomationRule>(`/api/rules/${id}`, data).then((response) => response.data),
  remove: (id: string) => api.delete(`/api/rules/${id}`),
}
```

---

## Step 10 - Authenticated App Layout

### `pasukhi-admin/src/components/layout/app-layout.tsx`

```tsx
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { authApi } from '../../api/auth'
import { Button } from '../ui/button'
import { useAuthStore } from '../../stores/auth-store'

const navItems = [
  { to: '/', label: 'Dashboard' },
  { to: '/channels', label: 'Channels' },
  { to: '/faqs', label: 'FAQs' },
  { to: '/rules', label: 'Rules' },
]

export function AppLayout() {
  const navigate = useNavigate()
  const user = useAuthStore((state) => state.user)
  const clearAuth = useAuthStore((state) => state.clearAuth)

  const signOut = async () => {
    try {
      await authApi.logout()
    } finally {
      clearAuth()
      navigate('/login', { replace: true })
    }
  }

  return (
    <div className="min-h-screen bg-gray-50 text-gray-950">
      <header className="border-b bg-white">
        <div className="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-xl font-semibold">Pasukhi Admin</p>
            <p className="text-sm text-gray-500">{user?.email}</p>
          </div>
          <nav className="flex flex-wrap gap-2">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  `rounded-md px-3 py-2 text-sm font-medium ${
                    isActive ? 'bg-gray-900 text-white' : 'text-gray-600 hover:bg-gray-100'
                  }`
                }
              >
                {item.label}
              </NavLink>
            ))}
            <Button type="button" variant="outline" onClick={signOut}>
              Sign out
            </Button>
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-8">
        <Outlet />
      </main>
    </div>
  )
}
```

---

## Step 11 - Channels Page

### `pasukhi-admin/src/features/channels/channels-page.tsx`

```tsx
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { useState } from 'react'
import { toast } from 'sonner'
import { channelsApi } from '../../api/channels'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import {
  channelTypeLabels,
  type ChannelConnection,
  type ChannelType,
  type SaveChannelConnectionRequest,
} from '../../types/channel'

const emptyForm: SaveChannelConnectionRequest = {
  channelType: 0,
  externalAccountId: '',
  externalAccountName: '',
  accessToken: '',
  verifyToken: '',
  isActive: true,
}

export function ChannelsPage() {
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<ChannelConnection | null>(null)
  const [form, setForm] = useState<SaveChannelConnectionRequest>({ ...emptyForm })

  const channelsQuery = useQuery({
    queryKey: ['channels'],
    queryFn: channelsApi.list,
  })

  const saveMutation = useMutation({
    mutationFn: () =>
      editing
        ? channelsApi.update(editing.id, normalizeForm(form))
        : channelsApi.create(normalizeForm(form)),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['channels'] })
      toast.success(editing ? 'Channel updated' : 'Channel created')
      resetForm()
    },
  })

  const deleteMutation = useMutation({
    mutationFn: channelsApi.remove,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['channels'] })
      toast.success('Channel deleted')
    },
  })

  const resetForm = () => {
    setEditing(null)
    setForm({ ...emptyForm })
  }

  const startEdit = (channel: ChannelConnection) => {
    setEditing(channel)
    setForm({
      channelType: channel.channelType,
      externalAccountId: channel.externalAccountId,
      externalAccountName: channel.externalAccountName ?? '',
      accessToken: channel.accessToken,
      verifyToken: channel.verifyToken,
      isActive: channel.isActive,
    })
  }

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    saveMutation.mutate()
  }

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold">Channels</h1>
        <p className="text-sm text-gray-500">Connect each Meta channel account once.</p>
      </div>

      <form onSubmit={onSubmit} className="grid gap-4 rounded-lg border bg-white p-4 md:grid-cols-2">
        <div>
          <Label htmlFor="channelType">Channel</Label>
          <select
            id="channelType"
            className="mt-1 h-9 w-full rounded-md border px-3 text-sm"
            value={form.channelType}
            disabled={Boolean(editing)}
            onChange={(event) =>
              setForm({ ...form, channelType: Number(event.target.value) as ChannelType })
            }
          >
            {Object.entries(channelTypeLabels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <Label htmlFor="externalAccountName">Account name</Label>
          <Input
            id="externalAccountName"
            value={form.externalAccountName ?? ''}
            onChange={(event) => setForm({ ...form, externalAccountName: event.target.value })}
          />
        </div>

        <div>
          <Label htmlFor="externalAccountId">External account ID</Label>
          <Input
            id="externalAccountId"
            value={form.externalAccountId}
            onChange={(event) => setForm({ ...form, externalAccountId: event.target.value })}
            required
          />
        </div>

        <div>
          <Label htmlFor="accessToken">Access token</Label>
          <Input
            id="accessToken"
            type="password"
            value={form.accessToken}
            onChange={(event) => setForm({ ...form, accessToken: event.target.value })}
            required
          />
        </div>

        <div>
          <Label htmlFor="verifyToken">Verify token</Label>
          <Input
            id="verifyToken"
            value={form.verifyToken ?? ''}
            onChange={(event) => setForm({ ...form, verifyToken: event.target.value })}
            placeholder="Generated when left blank"
          />
        </div>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={form.isActive}
            onChange={(event) => setForm({ ...form, isActive: event.target.checked })}
          />
          Active
        </label>

        <div className="flex gap-2 md:col-span-2">
          <Button type="submit" disabled={saveMutation.isPending}>
            {editing ? 'Save channel' : 'Create channel'}
          </Button>
          {editing && (
            <Button type="button" variant="outline" onClick={resetForm}>
              Cancel
            </Button>
          )}
        </div>
      </form>

      <div className="overflow-hidden rounded-lg border bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-gray-100 text-gray-600">
            <tr>
              <th className="px-4 py-3">Channel</th>
              <th className="px-4 py-3">Account</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Webhook</th>
              <th className="px-4 py-3"></th>
            </tr>
          </thead>
          <tbody>
            {channelsQuery.data?.map((channel) => (
              <tr key={channel.id} className="border-t">
                <td className="px-4 py-3">{channelTypeLabels[channel.channelType]}</td>
                <td className="px-4 py-3">
                  <div className="font-medium">{channel.externalAccountName || 'Unnamed'}</div>
                  <div className="text-xs text-gray-500">{channel.externalAccountId}</div>
                </td>
                <td className="px-4 py-3">{channel.isActive ? 'Active' : 'Paused'}</td>
                <td className="px-4 py-3">
                  {channel.lastWebhookAt ? new Date(channel.lastWebhookAt).toLocaleString() : 'Never'}
                </td>
                <td className="space-x-2 px-4 py-3 text-right">
                  <Button type="button" variant="outline" onClick={() => startEdit(channel)}>
                    Edit
                  </Button>
                  <Button
                    type="button"
                    variant="destructive"
                    onClick={() => deleteMutation.mutate(channel.id)}
                    disabled={deleteMutation.isPending}
                  >
                    Delete
                  </Button>
                </td>
              </tr>
            ))}
            {channelsQuery.data?.length === 0 && (
              <tr>
                <td className="px-4 py-6 text-center text-gray-500" colSpan={5}>
                  No channels yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function normalizeForm(form: SaveChannelConnectionRequest): SaveChannelConnectionRequest {
  return {
    ...form,
    externalAccountId: form.externalAccountId.trim(),
    externalAccountName: form.externalAccountName?.trim() || null,
    accessToken: form.accessToken.trim(),
    verifyToken: form.verifyToken?.trim() || null,
  }
}
```

---

## Step 12 - FAQs Page

### `pasukhi-admin/src/features/faqs/faqs-page.tsx`

```tsx
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { useState } from 'react'
import { toast } from 'sonner'
import { faqsApi } from '../../api/faqs'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import type { FaqItem, SaveFaqItemRequest } from '../../types/faq'

const emptyForm: SaveFaqItemRequest = {
  question: '',
  answer: '',
  keywords: '',
  isActive: true,
  sortOrder: 0,
}

export function FaqsPage() {
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<FaqItem | null>(null)
  const [form, setForm] = useState<SaveFaqItemRequest>({ ...emptyForm })

  const faqsQuery = useQuery({
    queryKey: ['faqs'],
    queryFn: faqsApi.list,
  })

  const saveMutation = useMutation({
    mutationFn: () =>
      editing ? faqsApi.update(editing.id, normalizeForm(form)) : faqsApi.create(normalizeForm(form)),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['faqs'] })
      toast.success(editing ? 'FAQ updated' : 'FAQ created')
      resetForm()
    },
  })

  const deleteMutation = useMutation({
    mutationFn: faqsApi.remove,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['faqs'] })
      toast.success('FAQ deleted')
    },
  })

  const resetForm = () => {
    setEditing(null)
    setForm({ ...emptyForm })
  }

  const startEdit = (faq: FaqItem) => {
    setEditing(faq)
    setForm({
      question: faq.question,
      answer: faq.answer,
      keywords: faq.keywords ?? '',
      isActive: faq.isActive,
      sortOrder: faq.sortOrder,
    })
  }

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    saveMutation.mutate()
  }

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold">FAQs</h1>
        <p className="text-sm text-gray-500">Reusable answers for common customer questions.</p>
      </div>

      <form onSubmit={onSubmit} className="space-y-4 rounded-lg border bg-white p-4">
        <div className="grid gap-4 md:grid-cols-[1fr_160px]">
          <div>
            <Label htmlFor="question">Question</Label>
            <Input
              id="question"
              value={form.question}
              onChange={(event) => setForm({ ...form, question: event.target.value })}
              required
            />
          </div>
          <div>
            <Label htmlFor="sortOrder">Sort order</Label>
            <Input
              id="sortOrder"
              type="number"
              min={0}
              value={form.sortOrder}
              onChange={(event) => setForm({ ...form, sortOrder: Number(event.target.value) })}
            />
          </div>
        </div>

        <div>
          <Label htmlFor="answer">Answer</Label>
          <textarea
            id="answer"
            className="mt-1 min-h-28 w-full rounded-md border px-3 py-2 text-sm"
            value={form.answer}
            onChange={(event) => setForm({ ...form, answer: event.target.value })}
            required
          />
        </div>

        <div>
          <Label htmlFor="keywords">Keywords</Label>
          <Input
            id="keywords"
            value={form.keywords ?? ''}
            onChange={(event) => setForm({ ...form, keywords: event.target.value })}
            placeholder="shipping, price, delivery"
          />
        </div>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={form.isActive}
            onChange={(event) => setForm({ ...form, isActive: event.target.checked })}
          />
          Active
        </label>

        <div className="flex gap-2">
          <Button type="submit" disabled={saveMutation.isPending}>
            {editing ? 'Save FAQ' : 'Create FAQ'}
          </Button>
          {editing && (
            <Button type="button" variant="outline" onClick={resetForm}>
              Cancel
            </Button>
          )}
        </div>
      </form>

      <div className="space-y-3">
        {faqsQuery.data?.map((faq) => (
          <article key={faq.id} className="rounded-lg border bg-white p-4">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <h2 className="font-semibold">{faq.question}</h2>
                <p className="mt-2 whitespace-pre-wrap text-sm text-gray-700">{faq.answer}</p>
                <p className="mt-2 text-xs text-gray-500">
                  {faq.keywords || 'No keywords'} | matches: {faq.matchCount} | order: {faq.sortOrder}
                </p>
              </div>
              <div className="flex shrink-0 gap-2">
                <Button type="button" variant="outline" onClick={() => startEdit(faq)}>
                  Edit
                </Button>
                <Button
                  type="button"
                  variant="destructive"
                  onClick={() => deleteMutation.mutate(faq.id)}
                  disabled={deleteMutation.isPending}
                >
                  Delete
                </Button>
              </div>
            </div>
          </article>
        ))}
        {faqsQuery.data?.length === 0 && (
          <div className="rounded-lg border bg-white p-6 text-center text-sm text-gray-500">
            No FAQs yet.
          </div>
        )}
      </div>
    </div>
  )
}

function normalizeForm(form: SaveFaqItemRequest): SaveFaqItemRequest {
  return {
    ...form,
    question: form.question.trim(),
    answer: form.answer.trim(),
    keywords: form.keywords?.trim() || null,
  }
}
```

---

## Step 13 - Rules Page

### `pasukhi-admin/src/features/rules/rules-page.tsx`

```tsx
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { useState } from 'react'
import { toast } from 'sonner'
import { rulesApi } from '../../api/rules'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import {
  actionTypeLabels,
  triggerTypeLabels,
  type ActionType,
  type AutomationRule,
  type SaveAutomationRuleRequest,
  type TriggerType,
} from '../../types/rule'

const emptyForm: SaveAutomationRuleRequest = {
  name: '',
  priority: 0,
  triggerType: 0,
  triggerValue: '',
  actionType: 0,
  actionValue: '',
  isActive: true,
}

export function RulesPage() {
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<AutomationRule | null>(null)
  const [form, setForm] = useState<SaveAutomationRuleRequest>({ ...emptyForm })

  const rulesQuery = useQuery({
    queryKey: ['rules'],
    queryFn: rulesApi.list,
  })

  const saveMutation = useMutation({
    mutationFn: () =>
      editing ? rulesApi.update(editing.id, normalizeForm(form)) : rulesApi.create(normalizeForm(form)),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['rules'] })
      toast.success(editing ? 'Rule updated' : 'Rule created')
      resetForm()
    },
  })

  const deleteMutation = useMutation({
    mutationFn: rulesApi.remove,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['rules'] })
      toast.success('Rule deleted')
    },
  })

  const resetForm = () => {
    setEditing(null)
    setForm({ ...emptyForm })
  }

  const startEdit = (rule: AutomationRule) => {
    setEditing(rule)
    setForm({
      name: rule.name,
      priority: rule.priority,
      triggerType: rule.triggerType,
      triggerValue: rule.triggerValue,
      actionType: rule.actionType,
      actionValue: rule.actionValue,
      isActive: rule.isActive,
    })
  }

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    saveMutation.mutate()
  }

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold">Rules</h1>
        <p className="text-sm text-gray-500">Deterministic automation before AI enters the pipeline.</p>
      </div>

      <form onSubmit={onSubmit} className="space-y-4 rounded-lg border bg-white p-4">
        <div className="grid gap-4 md:grid-cols-[1fr_160px]">
          <div>
            <Label htmlFor="name">Name</Label>
            <Input
              id="name"
              value={form.name}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
              required
            />
          </div>
          <div>
            <Label htmlFor="priority">Priority</Label>
            <Input
              id="priority"
              type="number"
              min={0}
              value={form.priority}
              onChange={(event) => setForm({ ...form, priority: Number(event.target.value) })}
            />
          </div>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <div>
            <Label htmlFor="triggerType">Trigger</Label>
            <select
              id="triggerType"
              className="mt-1 h-9 w-full rounded-md border px-3 text-sm"
              value={form.triggerType}
              onChange={(event) =>
                setForm({ ...form, triggerType: Number(event.target.value) as TriggerType })
              }
            >
              {Object.entries(triggerTypeLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </div>

          <div>
            <Label htmlFor="actionType">Action</Label>
            <select
              id="actionType"
              className="mt-1 h-9 w-full rounded-md border px-3 text-sm"
              value={form.actionType}
              onChange={(event) =>
                setForm({ ...form, actionType: Number(event.target.value) as ActionType })
              }
            >
              {Object.entries(actionTypeLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div>
          <Label htmlFor="triggerValue">Trigger value</Label>
          <Input
            id="triggerValue"
            value={form.triggerValue}
            onChange={(event) => setForm({ ...form, triggerValue: event.target.value })}
            placeholder="keyword list, regex, Text, or 09:00-18:00"
            required
          />
        </div>

        <div>
          <Label htmlFor="actionValue">Action value</Label>
          <textarea
            id="actionValue"
            className="mt-1 min-h-24 w-full rounded-md border px-3 py-2 text-sm"
            value={form.actionValue}
            onChange={(event) => setForm({ ...form, actionValue: event.target.value })}
            required
          />
        </div>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={form.isActive}
            onChange={(event) => setForm({ ...form, isActive: event.target.checked })}
          />
          Active
        </label>

        <div className="flex gap-2">
          <Button type="submit" disabled={saveMutation.isPending}>
            {editing ? 'Save rule' : 'Create rule'}
          </Button>
          {editing && (
            <Button type="button" variant="outline" onClick={resetForm}>
              Cancel
            </Button>
          )}
        </div>
      </form>

      <div className="overflow-hidden rounded-lg border bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-gray-100 text-gray-600">
            <tr>
              <th className="px-4 py-3">Priority</th>
              <th className="px-4 py-3">Rule</th>
              <th className="px-4 py-3">Trigger</th>
              <th className="px-4 py-3">Action</th>
              <th className="px-4 py-3">Matches</th>
              <th className="px-4 py-3"></th>
            </tr>
          </thead>
          <tbody>
            {rulesQuery.data?.map((rule) => (
              <tr key={rule.id} className="border-t">
                <td className="px-4 py-3">{rule.priority}</td>
                <td className="px-4 py-3">
                  <div className="font-medium">{rule.name}</div>
                  <div className="text-xs text-gray-500">{rule.isActive ? 'Active' : 'Paused'}</div>
                </td>
                <td className="px-4 py-3">
                  <div>{triggerTypeLabels[rule.triggerType]}</div>
                  <div className="max-w-xs truncate text-xs text-gray-500">{rule.triggerValue}</div>
                </td>
                <td className="px-4 py-3">
                  <div>{actionTypeLabels[rule.actionType]}</div>
                  <div className="max-w-xs truncate text-xs text-gray-500">{rule.actionValue}</div>
                </td>
                <td className="px-4 py-3">{rule.matchCount}</td>
                <td className="space-x-2 px-4 py-3 text-right">
                  <Button type="button" variant="outline" onClick={() => startEdit(rule)}>
                    Edit
                  </Button>
                  <Button
                    type="button"
                    variant="destructive"
                    onClick={() => deleteMutation.mutate(rule.id)}
                    disabled={deleteMutation.isPending}
                  >
                    Delete
                  </Button>
                </td>
              </tr>
            ))}
            {rulesQuery.data?.length === 0 && (
              <tr>
                <td className="px-4 py-6 text-center text-gray-500" colSpan={6}>
                  No rules yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function normalizeForm(form: SaveAutomationRuleRequest): SaveAutomationRuleRequest {
  return {
    ...form,
    name: form.name.trim(),
    triggerValue: form.triggerValue.trim(),
    actionValue: form.actionValue.trim(),
  }
}
```

---

## Step 14 - Update Routes

Update the app routes to render the authenticated layout and the new pages.

### `pasukhi-admin/src/App.tsx`

```tsx
import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/layout/app-layout'
import { AuthGuard } from './components/layout/auth-guard'
import { ChannelsPage } from './features/channels/channels-page'
import { FaqsPage } from './features/faqs/faqs-page'
import { LoginPage } from './features/auth/login-page'
import { RulesPage } from './features/rules/rules-page'

function DashboardPage() {
  return (
    <div className="space-y-2">
      <h1 className="text-2xl font-semibold">Dashboard</h1>
      <p className="text-sm text-gray-500">Configure the automation foundation before webhooks are connected.</p>
    </div>
  )
}

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<AuthGuard />}>
        <Route element={<AppLayout />}>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/channels" element={<ChannelsPage />} />
          <Route path="/faqs" element={<FaqsPage />} />
          <Route path="/rules" element={<RulesPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
```

---

## Step 15 - Verify

Run the compile checks first.

```bash
dotnet build
cd pasukhi-admin
npx tsc --noEmit
npm run build
```

If PostgreSQL is available, apply migrations and start the API.

```bash
dotnet ef database update --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
dotnet run --project src/Pasukhi.API
```

In a second PowerShell window, test the new endpoints.

```powershell
$login = Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5000/api/auth/login `
  -ContentType 'application/json' `
  -Body '{"email":"admin@pasukhi.ge","password":"Admin@123!"}'

$headers = @{ Authorization = "Bearer $($login.accessToken)" }

Invoke-RestMethod -Uri http://localhost:5000/api/channels -Headers $headers
Invoke-RestMethod -Uri http://localhost:5000/api/faqs -Headers $headers
Invoke-RestMethod -Uri http://localhost:5000/api/rules -Headers $headers
```

Expected result:
- Backend build has 0 errors
- Frontend typecheck has 0 errors
- Frontend production build succeeds
- Login returns an access token
- Channels, FAQs, and Rules list endpoints return empty arrays for a fresh database
- The frontend can open `/channels`, `/faqs`, and `/rules` after login

---

## Commit

```bash
git add src/ pasukhi-admin/ docs/codex/phase-2.md
git commit -m "feat(02-01): channel FAQ rule CRUD and matchers"
```

---

## What's Next

Phase 3: `docs/codex/phase-3.md` - Webhook controllers, signature verification, Meta App setup, and ngrok.
