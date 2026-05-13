import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, Bot, Clock, Download, GitBranch, MessageCircle, SlidersHorizontal } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { analyticsApi } from '../../api/analytics'
import { Button } from '../../components/ui/button'
import { channelTypeLabels, channelTypes } from '../../types/channel'
import type { DailyBreakdown } from '../../types/analytics'

const ranges = [7, 30] as const

export function DashboardPage() {
  const [days, setDays] = useState<(typeof ranges)[number]>(7)
  const navigate = useNavigate()
  const dashboardQuery = useQuery({
    queryKey: ['dashboard', days],
    queryFn: () => analyticsApi.getDashboard(days),
  })

  const stats = dashboardQuery.data
  const autoReplies = (stats?.faqReplies ?? 0) + (stats?.ruleReplies ?? 0) + (stats?.aiReplies ?? 0)

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <div className="text-[12px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            Wednesday · May 13
          </div>
          <h2 className="mt-1 text-[26px] font-semibold tracking-tight text-slate-950">გამარჯობა, Nika</h2>
          <p className="mt-1 text-[13.5px] text-slate-500">
            Here&apos;s how <span className="font-medium text-slate-900">Khinkali House</span>&apos;s inbox is doing today.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {ranges.map((range) => (
            <Button
              key={range}
              type="button"
              variant={days === range ? 'default' : 'outline'}
              onClick={() => setDays(range)}
            >
              {range} days
            </Button>
          ))}
          <Button type="button" variant="outline">
            <Download className="size-4" />
            Export report
          </Button>
          <Button type="button" onClick={() => navigate('/bot-readiness')}>
            <SlidersHorizontal className="size-4" />
            Tune the bot
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard
          label="Conversations"
          value={stats?.totalInbound}
          sub={`${formatNumber(stats?.totalOutbound ?? 0)} outbound replies`}
          icon={MessageCircle}
          loading={dashboardQuery.isLoading}
        />
        <KpiCard
          label="Bot resolution rate"
          value={stats ? `${Math.round(stats.autoReplyRate * 100)}%` : undefined}
          sub={`${formatNumber(autoReplies)} auto replies`}
          icon={Bot}
          tone="emerald"
          loading={dashboardQuery.isLoading}
        />
        <KpiCard
          label="AI tokens"
          value={stats?.aiTokensUsed}
          sub={`${formatNumber(stats?.aiReplies ?? 0)} AI replies`}
          icon={Clock}
          tone="amber"
          loading={dashboardQuery.isLoading}
        />
        <KpiCard
          label="Escalations"
          value={stats?.escalations}
          sub="Needs human attention"
          icon={AlertTriangle}
          tone="rose"
          loading={dashboardQuery.isLoading}
        />
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <section className="card-shadow rounded-2xl border border-border bg-white p-5 lg:col-span-2">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-[13px] font-semibold text-slate-950">Conversation volume</h3>
              <p className="text-[12px] text-slate-500">Inbound and outbound messages over time</p>
            </div>
            <div className="hidden items-center gap-3 text-[11.5px] sm:flex">
              <span className="inline-flex items-center gap-1.5 text-slate-600">
                <span className="size-2 rounded-sm bg-primary" />
                Inbound
              </span>
              <span className="inline-flex items-center gap-1.5 text-slate-600">
                <span className="size-2 rounded-sm bg-slate-400" />
                Outbound
              </span>
            </div>
          </div>
          <div className="mt-5">
            <VolumeChart rows={stats?.dailyBreakdown ?? []} loading={dashboardQuery.isLoading} />
          </div>
        </section>

        <section className="card-shadow rounded-2xl border border-border bg-white p-5">
          <h3 className="text-[13px] font-semibold text-slate-950">Channels</h3>
          <p className="text-[12px] text-slate-500">Live connection status</p>
          <div className="mt-4 space-y-3">
            {channelTypes.map((channelType) => {
              const row = stats?.channelBreakdown.find((item) => item.channelType === channelType)
              return (
                <div key={channelType} className="flex items-center gap-3">
                  <div className="flex size-8 items-center justify-center rounded-lg bg-indigo-50 text-indigo-700">
                    <GitBranch className="size-4" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-[13px] font-medium text-slate-950">
                      {channelTypeLabels[channelType]}
                    </div>
                    <div className="text-[11.5px] text-slate-500">{formatNumber(row?.totalInbound ?? 0)} inbound</div>
                  </div>
                  <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-medium text-emerald-700">
                    <span className="size-1.5 rounded-full bg-emerald-500" />
                    Live
                  </span>
                </div>
              )
            })}
          </div>
          <Button type="button" variant="outline" className="mt-4 w-full border-dashed">
            Connect a channel
          </Button>
        </section>
      </div>

      <section className="card-shadow overflow-hidden rounded-2xl border border-border bg-white">
        <div className="flex items-center justify-between p-5 pb-3">
          <div>
            <h3 className="text-[13px] font-semibold text-slate-950">Daily breakdown</h3>
            <p className="text-[12px] text-slate-500">Automation performance by day</p>
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-[13px]">
            <thead className="border-y border-border bg-muted/50 text-[11px] uppercase tracking-[0.14em] text-slate-500">
              <tr>
                <th className="px-5 py-3 font-semibold">Date</th>
                <th className="px-5 py-3 font-semibold">Inbound</th>
                <th className="px-5 py-3 font-semibold">Outbound</th>
                <th className="px-5 py-3 font-semibold">Auto replies</th>
                <th className="px-5 py-3 font-semibold">AI tokens</th>
                <th className="px-5 py-3 font-semibold">Escalations</th>
              </tr>
            </thead>
            <tbody>
              {(stats?.dailyBreakdown ?? []).slice().reverse().map((row) => (
                <tr key={row.date} className="border-b border-border/70 last:border-0">
                  <td className="px-5 py-3 font-medium text-slate-900">{formatDate(row.date)}</td>
                  <td className="px-5 py-3 tabular-nums text-slate-600">{formatNumber(row.totalInbound)}</td>
                  <td className="px-5 py-3 tabular-nums text-slate-600">{formatNumber(row.totalOutbound)}</td>
                  <td className="px-5 py-3 tabular-nums text-slate-600">
                    {formatNumber(row.faqReplies + row.ruleReplies + row.aiReplies)}
                  </td>
                  <td className="px-5 py-3 tabular-nums text-slate-600">{formatNumber(row.aiTokensUsed)}</td>
                  <td className="px-5 py-3 tabular-nums text-slate-600">{formatNumber(row.escalations)}</td>
                </tr>
              ))}
              {!dashboardQuery.isLoading && (stats?.dailyBreakdown.length ?? 0) === 0 && (
                <tr>
                  <td colSpan={6} className="px-5 py-10 text-center text-slate-500">
                    No daily activity yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}

function KpiCard({
  label,
  value,
  sub,
  icon: Icon,
  tone = 'indigo',
  loading,
}: {
  label: string
  value?: number | string
  sub: string
  icon: typeof MessageCircle
  tone?: 'indigo' | 'emerald' | 'amber' | 'rose'
  loading: boolean
}) {
  const tones = {
    indigo: 'bg-indigo-50 text-indigo-700',
    emerald: 'bg-emerald-50 text-emerald-700',
    amber: 'bg-amber-50 text-amber-700',
    rose: 'bg-rose-50 text-rose-700',
  }

  return (
    <div className="card-shadow rounded-2xl border border-border bg-white p-5">
      <div className="flex items-start justify-between">
        <div className={`flex size-9 items-center justify-center rounded-xl ${tones[tone]}`}>
          <Icon className="size-4.5" />
        </div>
      </div>
      <div className="mt-4">
        <div className="text-[12px] font-semibold uppercase tracking-[0.14em] text-slate-500">{label}</div>
        <div className="mt-1 text-3xl font-semibold tracking-tight text-slate-950 tabular-nums">
          {loading ? <span className="block h-8 w-20 rounded-lg bg-muted" /> : value ?? 0}
        </div>
        <div className="mt-1 text-[12.5px] text-slate-500">{sub}</div>
      </div>
    </div>
  )
}

function VolumeChart({ rows, loading }: { rows: DailyBreakdown[]; loading: boolean }) {
  const chart = useMemo(() => {
    const data = rows.length
      ? rows
      : Array.from({ length: 7 }, (_, index) => ({
          date: `2026-05-${String(index + 1).padStart(2, '0')}`,
          totalInbound: 0,
          totalOutbound: 0,
          faqReplies: 0,
          ruleReplies: 0,
          aiReplies: 0,
          aiTokensUsed: 0,
          escalations: 0,
        }))
    const width = 720
    const height = 200
    const pad = 28
    const max = Math.max(1, ...data.flatMap((row) => [row.totalInbound, row.totalOutbound]))
    const x = (index: number) => pad + (index / Math.max(1, data.length - 1)) * (width - pad * 2)
    const y = (value: number) => height - pad - (value / max) * (height - pad * 2)
    const pathFor = (values: number[]) =>
      values.map((value, index) => `${index ? 'L' : 'M'}${x(index).toFixed(1)},${y(value).toFixed(1)}`).join(' ')
    return {
      width,
      height,
      inbound: pathFor(data.map((row) => row.totalInbound)),
      outbound: pathFor(data.map((row) => row.totalOutbound)),
      area: `${pathFor(data.map((row) => row.totalInbound))} L ${x(data.length - 1)},${height - pad} L ${x(0)},${height - pad} Z`,
    }
  }, [rows])

  if (loading) return <div className="h-[200px] rounded-xl bg-muted" />

  return (
    <svg viewBox={`0 0 ${chart.width} ${chart.height}`} className="w-full">
      <defs>
        <linearGradient id="volumeFill" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="var(--primary)" stopOpacity="0.24" />
          <stop offset="100%" stopColor="var(--primary)" stopOpacity="0" />
        </linearGradient>
      </defs>
      {[0.25, 0.5, 0.75, 1].map((pct) => (
        <line
          key={pct}
          x1="28"
          x2={chart.width - 28}
          y1={chart.height - 28 - pct * (chart.height - 56)}
          y2={chart.height - 28 - pct * (chart.height - 56)}
          stroke="#eef2f4"
          strokeDasharray="3 3"
        />
      ))}
      <path d={chart.area} fill="url(#volumeFill)" />
      <path d={chart.outbound} fill="none" stroke="#94a3b8" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <path d={chart.inbound} fill="none" stroke="var(--primary)" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function formatNumber(value: number) {
  return new Intl.NumberFormat().format(value)
}

function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString()
}
