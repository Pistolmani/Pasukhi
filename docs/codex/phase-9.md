# Codex Task — Phase 9: Settings Service + Working Hours

> Read `AGENTS.md` first. Phases 0–8 must be complete before starting this.

## Goal

By the end of this phase:
- `BusinessSetting` entity stores per-tenant key-value configuration in the database
- `SettingsService` replaces the Phase 7 `DefaultSettingsService` stub
- `AutoReplyEnabled`, `WorkingHoursEnabled`, `WorkingHoursStart`, `WorkingHoursEnd`, and `Timezone` are persisted per tenant
- `GET /api/settings` and `PUT /api/settings` let operators configure these values
- The frontend has a Settings page

---

## Repo root

`C:\Users\piros\OneDrive\Desktop\Pasukhi\`

---

## Step 1 — BusinessSetting Entity

### `src/Pasukhi.Domain/Entities/BusinessSetting.cs`

```csharp
namespace Pasukhi.Domain.Entities;

public class BusinessSetting : TenantEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
```

Add to `PasukhiDbContext`:

```csharp
public DbSet<BusinessSetting> BusinessSettings => Set<BusinessSetting>();
```

In `OnModelCreating`:

```csharp
modelBuilder.Entity<BusinessSetting>()
    .HasQueryFilter(s => s.BusinessId == _tenantProvider.BusinessId);

modelBuilder.Entity<BusinessSetting>()
    .HasIndex(s => new { s.BusinessId, s.Key })
    .IsUnique();
```

---

## Step 2 — Migration

```bash
dotnet ef migrations add AddBusinessSettings --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
dotnet ef database update --project src/Pasukhi.Infrastructure --startup-project src/Pasukhi.API
```

---

## Step 3 — Real SettingsService

Replace the `DefaultSettingsService` stub with this DB-backed implementation.

### `src/Pasukhi.Infrastructure/Services/SettingsService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Settings;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private const string DefaultWorkingHoursStart = "09:00";
    private const string DefaultWorkingHoursEnd = "18:00";
    private const string DefaultTimezone = "Asia/Tbilisi";
    private readonly PasukhiDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public SettingsService(PasukhiDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task<BusinessSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await _db.BusinessSettings
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        return Map(settings);
    }

    public async Task<BusinessSettingsDto> UpdateAsync(UpdateBusinessSettingsRequest request, CancellationToken ct = default)
    {
        var businessId = _tenantProvider.BusinessId == Guid.Empty
            ? throw new InvalidOperationException("Tenant context is required.")
            : _tenantProvider.BusinessId;

        var settings = await _db.BusinessSettings.ToDictionaryAsync(s => s.Key, ct);

        Upsert(settings, businessId, SettingKeys.AutoReplyEnabled, request.AutoReplyEnabled.ToString());
        Upsert(settings, businessId, SettingKeys.WorkingHoursEnabled, request.WorkingHoursEnabled.ToString());
        Upsert(settings, businessId, SettingKeys.WorkingHoursStart, NormalizeTime(request.WorkingHoursStart, DefaultWorkingHoursStart));
        Upsert(settings, businessId, SettingKeys.WorkingHoursEnd, NormalizeTime(request.WorkingHoursEnd, DefaultWorkingHoursEnd));
        Upsert(settings, businessId, SettingKeys.Timezone, NormalizeText(request.Timezone, DefaultTimezone));

        await _db.SaveChangesAsync(ct);

        return Map(settings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value));
    }

    private static BusinessSettingsDto Map(Dictionary<string, string> settings) =>
        new(
            ReadBool(settings, SettingKeys.AutoReplyEnabled, defaultValue: true),
            ReadBool(settings, SettingKeys.WorkingHoursEnabled, defaultValue: false),
            ReadString(settings, SettingKeys.WorkingHoursStart, DefaultWorkingHoursStart),
            ReadString(settings, SettingKeys.WorkingHoursEnd, DefaultWorkingHoursEnd),
            ReadString(settings, SettingKeys.Timezone, DefaultTimezone));

    private static bool ReadBool(Dictionary<string, string> settings, string key, bool defaultValue) =>
        settings.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : defaultValue;

    private static string ReadString(Dictionary<string, string> settings, string key, string defaultValue) =>
        settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : defaultValue;

    private void Upsert(Dictionary<string, BusinessSetting> settings, Guid businessId, string key, string value)
    {
        if (settings.TryGetValue(key, out var setting))
        {
            setting.Value = value;
            return;
        }

        setting = new BusinessSetting { Id = Guid.NewGuid(), BusinessId = businessId, Key = key, Value = value };
        _db.BusinessSettings.Add(setting);
        settings[key] = setting;
    }

    private static string NormalizeTime(string value, string fallback) =>
        TimeOnly.TryParse(value, out var time) ? time.ToString("HH:mm") : fallback;

    private static string NormalizeText(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
```

---

## Step 4 — SettingsController

### `src/Pasukhi.API/Controllers/SettingsController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Settings;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;

    public SettingsController(ISettingsService settings)
    {
        _settings = settings;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await _settings.GetAsync(cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateBusinessSettingsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _settings.UpdateAsync(request, cancellationToken));
}
```

---

## Step 5 — Replace Stub in Program.cs

Remove the `DefaultSettingsService` registration and replace with the real one:

```csharp
// Remove:
// builder.Services.AddScoped<ISettingsService, DefaultSettingsService>();

// Add:
builder.Services.AddScoped<ISettingsService, SettingsService>();
```

You can also delete `src/Pasukhi.Infrastructure/Services/DefaultSettingsService.cs`.

---

## Step 6 — Frontend: Settings Page

### `pasukhi-admin/src/pages/Settings.tsx`

```tsx
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { api } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { toast } from 'sonner';

const schema = z.object({
  autoReplyEnabled: z.boolean(),
  workingHoursEnabled: z.boolean(),
  workingHoursStart: z.string().regex(/^\d{2}:\d{2}$/, 'Use HH:MM format'),
  workingHoursEnd: z.string().regex(/^\d{2}:\d{2}$/, 'Use HH:MM format'),
  timezone: z.string().min(1),
});

type SettingsForm = z.infer<typeof schema>;

export default function Settings() {
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery<SettingsForm>({
    queryKey: ['settings'],
    queryFn: () => api.get('/api/settings').then(r => r.data),
  });

  const { register, handleSubmit, setValue, watch, formState: { errors } } = useForm<SettingsForm>({
    resolver: zodResolver(schema),
    values: data,
  });

  const mutation = useMutation({
    mutationFn: (values: SettingsForm) => api.put('/api/settings', values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings'] });
      toast.success('Settings saved.');
    },
    onError: () => toast.error('Failed to save settings.'),
  });

  if (isLoading) return <div className="p-6">Loading…</div>;

  return (
    <form onSubmit={handleSubmit(values => mutation.mutate(values))} className="p-6 max-w-lg space-y-6">
      <h1 className="text-2xl font-semibold">Settings</h1>

      <div className="flex items-center gap-3">
        <Switch
          checked={watch('autoReplyEnabled')}
          onCheckedChange={v => setValue('autoReplyEnabled', v)}
        />
        <Label>Auto-reply enabled</Label>
      </div>

      <div className="flex items-center gap-3">
        <Switch
          checked={watch('workingHoursEnabled')}
          onCheckedChange={v => setValue('workingHoursEnabled', v)}
        />
        <Label>Working hours enabled</Label>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-1">
          <Label>Working hours start</Label>
          <Input {...register('workingHoursStart')} placeholder="09:00" />
          {errors.workingHoursStart && <p className="text-xs text-destructive">{errors.workingHoursStart.message}</p>}
        </div>
        <div className="space-y-1">
          <Label>Working hours end</Label>
          <Input {...register('workingHoursEnd')} placeholder="18:00" />
          {errors.workingHoursEnd && <p className="text-xs text-destructive">{errors.workingHoursEnd.message}</p>}
        </div>
      </div>

      <div className="space-y-1">
        <Label>Timezone</Label>
        <Input {...register('timezone')} placeholder="Asia/Tbilisi" />
      </div>

      <Button type="submit" disabled={mutation.isPending}>
        {mutation.isPending ? 'Saving…' : 'Save settings'}
      </Button>
    </form>
  );
}
```

Add route:

```tsx
<Route path="/settings" element={<Settings />} />
```

---

## Verification

```bash
dotnet build
cd pasukhi-admin && npx tsc --noEmit
```

Test:

```bash
# Disable auto-reply
curl -X PUT http://localhost:5000/api/settings \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"autoReplyEnabled":false,"workingHoursEnabled":false,"workingHoursStart":"09:00","workingHoursEnd":"18:00","timezone":"Asia/Tbilisi"}'

# Confirm subsequent webhooks skip automation (log: "auto-reply is disabled")
```

---

## Commit

```bash
git add src/ pasukhi-admin/src/ docs/codex/phase-9.md
git commit -m "feat(09): settings service + working hours enforcement"
```

---

## What's Next

Phase 10: `docs/codex/phase-10.md` — Analytics dashboard: `AnalyticsService`, `AnalyticsController`, and frontend Dashboard page.
