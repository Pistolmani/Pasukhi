# Codex Task — Phase 10: Analytics Dashboard + Admin Panel Polish

> Read `AGENTS.md` first. Phases 0–9 must be complete before starting this.

## Goal

By the end of this phase:
- `GET /api/analytics/dashboard?days=N` returns aggregated message and automation metrics for the last N days (1–90)
- Metrics are broken down by channel and by calendar day
- The frontend has a Dashboard page showing totals, auto-reply rate, and per-channel breakdown
- The frontend has an AI Prompt configuration page

---

## Repo root

`C:\Users\piros\OneDrive\Desktop\Pasukhi\`

---

## Step 1 — Analytics DTOs

### `src/Pasukhi.Application/DTOs/Analytics/AnalyticsDtos.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.DTOs.Analytics;

public record ChannelBreakdownDto(
    ChannelType ChannelType,
    int TotalInbound,
    int TotalOutbound,
    int FaqReplies,
    int RuleReplies,
    int AiReplies,
    int Escalations);

public record DailyBreakdownDto(
    DateOnly Date,
    int TotalInbound,
    int TotalOutbound,
    int FaqReplies,
    int RuleReplies,
    int AiReplies,
    int AiTokensUsed,
    int Escalations);

public record DashboardStatsDto(
    int TotalInbound,
    int TotalOutbound,
    int FaqReplies,
    int RuleReplies,
    int AiReplies,
    int AiTokensUsed,
    int Escalations,
    double AutoReplyRate,
    List<ChannelBreakdownDto> ChannelBreakdown,
    List<DailyBreakdownDto> DailyBreakdown);
```

---

## Step 2 — IAnalyticsService

### `src/Pasukhi.Application/Interfaces/IAnalyticsService.cs`

```csharp
using Pasukhi.Application.DTOs.Analytics;

namespace Pasukhi.Application.Interfaces;

public interface IAnalyticsService
{
    Task<DashboardStatsDto> GetDashboardAsync(int days = 7, CancellationToken ct = default);
}
```

---

## Step 3 — AnalyticsService

### `src/Pasukhi.Infrastructure/Services/AnalyticsService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Analytics;
using Pasukhi.Application.Interfaces;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly PasukhiDbContext _db;

    public AnalyticsService(PasukhiDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardStatsDto> GetDashboardAsync(int days = 7, CancellationToken ct = default)
    {
        var clampedDays = Math.Clamp(days, 1, 90);
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-clampedDays + 1));

        var metrics = await _db.DailyMetrics
            .AsNoTracking()
            .Where(m => m.Date >= cutoff)
            .ToListAsync(ct);

        var totalInbound = metrics.Sum(m => m.TotalInbound);
        var totalOutbound = metrics.Sum(m => m.TotalOutbound);
        var faqReplies = metrics.Sum(m => m.FaqReplies);
        var ruleReplies = metrics.Sum(m => m.RuleReplies);
        var aiReplies = metrics.Sum(m => m.AiReplies);
        var aiTokensUsed = metrics.Sum(m => m.AiTokensUsed);
        var escalations = metrics.Sum(m => m.Escalations);
        var autoReplies = faqReplies + ruleReplies + aiReplies;
        var autoReplyRate = totalInbound > 0 ? (double)autoReplies / totalInbound : 0;

        var channelBreakdown = metrics
            .Where(m => m.ChannelType.HasValue)
            .GroupBy(m => m.ChannelType!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new ChannelBreakdownDto(
                g.Key,
                g.Sum(m => m.TotalInbound),
                g.Sum(m => m.TotalOutbound),
                g.Sum(m => m.FaqReplies),
                g.Sum(m => m.RuleReplies),
                g.Sum(m => m.AiReplies),
                g.Sum(m => m.Escalations)))
            .ToList();

        var dailyBreakdown = metrics
            .GroupBy(m => m.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyBreakdownDto(
                g.Key,
                g.Sum(m => m.TotalInbound),
                g.Sum(m => m.TotalOutbound),
                g.Sum(m => m.FaqReplies),
                g.Sum(m => m.RuleReplies),
                g.Sum(m => m.AiReplies),
                g.Sum(m => m.AiTokensUsed),
                g.Sum(m => m.Escalations)))
            .ToList();

        return new DashboardStatsDto(
            totalInbound,
            totalOutbound,
            faqReplies,
            ruleReplies,
            aiReplies,
            aiTokensUsed,
            escalations,
            autoReplyRate,
            channelBreakdown,
            dailyBreakdown);
    }
}
```

---

## Step 4 — AnalyticsController

### `src/Pasukhi.API/Controllers/AnalyticsController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public AnalyticsController(IAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default) =>
        Ok(await _analytics.GetDashboardAsync(Math.Clamp(days, 1, 90), cancellationToken));
}
```

---

## Step 5 — Register Services in Program.cs

```csharp
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
```

---

## Step 6 — Frontend: Dashboard Page

### `pasukhi-admin/src/pages/Dashboard.tsx`

```tsx
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

interface DashboardStats {
  totalInbound: number;
  totalOutbound: number;
  faqReplies: number;
  ruleReplies: number;
  aiReplies: number;
  aiTokensUsed: number;
  escalations: number;
  autoReplyRate: number;
  channelBreakdown: {
    channelType: string;
    totalInbound: number;
    totalOutbound: number;
    faqReplies: number;
    ruleReplies: number;
    aiReplies: number;
    escalations: number;
  }[];
}

function StatCard({ title, value }: { title: string; value: string | number }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-2xl font-bold">{value}</p>
      </CardContent>
    </Card>
  );
}

export default function Dashboard() {
  const { data, isLoading } = useQuery<DashboardStats>({
    queryKey: ['dashboard'],
    queryFn: () => api.get('/api/analytics/dashboard?days=7').then(r => r.data),
    refetchInterval: 60_000,
  });

  if (isLoading) return <div className="p-6">Loading…</div>;
  if (!data) return null;

  const pct = (n: number) => `${(n * 100).toFixed(1)}%`;

  return (
    <div className="p-6 space-y-6">
      <h1 className="text-2xl font-semibold">Dashboard <span className="text-sm font-normal text-muted-foreground">(last 7 days)</span></h1>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatCard title="Messages received" value={data.totalInbound} />
        <StatCard title="Auto-reply rate" value={pct(data.autoReplyRate)} />
        <StatCard title="Escalations" value={data.escalations} />
        <StatCard title="AI tokens used" value={data.aiTokensUsed.toLocaleString()} />
      </div>

      <div className="grid grid-cols-3 gap-4">
        <StatCard title="FAQ replies" value={data.faqReplies} />
        <StatCard title="Rule replies" value={data.ruleReplies} />
        <StatCard title="AI replies" value={data.aiReplies} />
      </div>

      {data.channelBreakdown.length > 0 && (
        <div>
          <h2 className="text-lg font-semibold mb-3">By channel</h2>
          <div className="rounded-md border overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  {['Channel', 'Received', 'Sent', 'FAQ', 'Rules', 'AI', 'Escalations'].map(h => (
                    <th key={h} className="px-4 py-2 text-left font-medium">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y">
                {data.channelBreakdown.map(c => (
                  <tr key={c.channelType}>
                    <td className="px-4 py-2">{c.channelType}</td>
                    <td className="px-4 py-2">{c.totalInbound}</td>
                    <td className="px-4 py-2">{c.totalOutbound}</td>
                    <td className="px-4 py-2">{c.faqReplies}</td>
                    <td className="px-4 py-2">{c.ruleReplies}</td>
                    <td className="px-4 py-2">{c.aiReplies}</td>
                    <td className="px-4 py-2">{c.escalations}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
```

### `pasukhi-admin/src/pages/AiPrompt.tsx`

```tsx
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { api } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { toast } from 'sonner';

const schema = z.object({
  isAiEnabled: z.boolean(),
  systemPrompt: z.string().min(1, 'Required'),
  toneDescription: z.string().min(1, 'Required'),
  escalationMessage: z.string().min(1, 'Required'),
  maxAiTokensPerDay: z.number().min(0),
  aiConfidenceThreshold: z.number().min(0).max(1),
  faqConfidenceThreshold: z.number().min(0).max(1),
});

type AiPromptForm = z.infer<typeof schema>;

export default function AiPrompt() {
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery<AiPromptForm>({
    queryKey: ['ai-prompt'],
    queryFn: () => api.get('/api/ai/prompt').then(r => r.data).catch(() => ({
      isAiEnabled: false,
      systemPrompt: '',
      toneDescription: 'professional and friendly',
      escalationMessage: 'Let me connect you with our team.',
      maxAiTokensPerDay: 50000,
      aiConfidenceThreshold: 0.7,
      faqConfidenceThreshold: 0.85,
    })),
  });

  const { register, handleSubmit, setValue, watch, formState: { errors } } = useForm<AiPromptForm>({
    resolver: zodResolver(schema),
    values: data,
  });

  const mutation = useMutation({
    mutationFn: (values: AiPromptForm) => api.put('/api/ai/prompt', values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['ai-prompt'] });
      toast.success('AI prompt saved.');
    },
    onError: () => toast.error('Failed to save AI prompt.'),
  });

  if (isLoading) return <div className="p-6">Loading…</div>;

  return (
    <form onSubmit={handleSubmit(values => mutation.mutate(values))} className="p-6 max-w-2xl space-y-6">
      <h1 className="text-2xl font-semibold">AI Configuration</h1>

      <div className="flex items-center gap-3">
        <Switch checked={watch('isAiEnabled')} onCheckedChange={v => setValue('isAiEnabled', v)} />
        <Label>AI fallback enabled</Label>
      </div>

      <div className="space-y-1">
        <Label>System prompt</Label>
        <Textarea {...register('systemPrompt')} rows={5} placeholder="You are a helpful assistant for..." />
        {errors.systemPrompt && <p className="text-xs text-destructive">{errors.systemPrompt.message}</p>}
      </div>

      <div className="space-y-1">
        <Label>Tone description</Label>
        <Input {...register('toneDescription')} placeholder="professional and friendly" />
      </div>

      <div className="space-y-1">
        <Label>Escalation message</Label>
        <Input {...register('escalationMessage')} placeholder="Let me connect you with our team." />
      </div>

      <div className="grid grid-cols-3 gap-4">
        <div className="space-y-1">
          <Label>Max tokens/day</Label>
          <Input type="number" {...register('maxAiTokensPerDay', { valueAsNumber: true })} />
        </div>
        <div className="space-y-1">
          <Label>AI confidence threshold</Label>
          <Input type="number" step="0.05" {...register('aiConfidenceThreshold', { valueAsNumber: true })} />
        </div>
        <div className="space-y-1">
          <Label>FAQ confidence threshold</Label>
          <Input type="number" step="0.05" {...register('faqConfidenceThreshold', { valueAsNumber: true })} />
        </div>
      </div>

      <Button type="submit" disabled={mutation.isPending}>
        {mutation.isPending ? 'Saving…' : 'Save'}
      </Button>
    </form>
  );
}
```

Add routes:

```tsx
<Route path="/" element={<Dashboard />} />
<Route path="/ai" element={<AiPrompt />} />
```

---

## Verification

```bash
dotnet build
cd pasukhi-admin && npx tsc --noEmit
```

```bash
curl -H "Authorization: Bearer <token>" "http://localhost:5000/api/analytics/dashboard?days=7"
# Expected: DashboardStatsDto JSON with totals and breakdowns
```

---

## Commit

```bash
git add src/ pasukhi-admin/src/ docs/codex/phase-10.md
git commit -m "feat(10): analytics dashboard + admin panel AI prompt page"
```

---

## What's Next

Phase 11: `docs/codex/phase-11.md` — Dockerfile, Railway deployment config, and production `appsettings.Production.json`.
