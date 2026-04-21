import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { analyticsApi } from '../../api/analytics'
import { Button } from '../../components/ui/button'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../components/ui/table'
import { channelTypeLabels, channelTypes } from '../../types/channel'

const ranges = [7, 30] as const

export function DashboardPage() {
  const [days, setDays] = useState<(typeof ranges)[number]>(7)
  const dashboardQuery = useQuery({
    queryKey: ['dashboard', days],
    queryFn: () => analyticsApi.getDashboard(days),
  })

  const stats = dashboardQuery.data

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Dashboard</h1>
          <p className="text-muted-foreground text-sm">Message volume and automation performance.</p>
        </div>
        <div className="flex gap-2">
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
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
        <KpiCard label="Inbound" value={stats?.totalInbound} loading={dashboardQuery.isLoading} />
        <KpiCard label="Outbound" value={stats?.totalOutbound} loading={dashboardQuery.isLoading} />
        <KpiCard
          label="Auto-reply rate"
          value={stats ? `${Math.round(stats.autoReplyRate * 100)}%` : undefined}
          loading={dashboardQuery.isLoading}
        />
        <KpiCard label="FAQ replies" value={stats?.faqReplies} loading={dashboardQuery.isLoading} />
        <KpiCard label="AI replies" value={stats?.aiReplies} loading={dashboardQuery.isLoading} />
        <KpiCard label="Escalations" value={stats?.escalations} loading={dashboardQuery.isLoading} />
      </div>

      <section className="space-y-3">
        <h2 className="text-lg font-semibold">Channel Breakdown</h2>
        <div className="overflow-hidden rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Channel</TableHead>
                <TableHead>Inbound</TableHead>
                <TableHead>Outbound</TableHead>
                <TableHead>FAQ</TableHead>
                <TableHead>Rules</TableHead>
                <TableHead>AI</TableHead>
                <TableHead>Escalations</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {dashboardQuery.isLoading ? (
                <TableRow>
                  <TableCell colSpan={7} className="text-muted-foreground py-6 text-center">
                    Loading channel metrics...
                  </TableCell>
                </TableRow>
              ) : (
                channelTypes.map((channelType) => {
                  const row = stats?.channelBreakdown.find((item) => item.channelType === channelType)
                  return (
                    <TableRow key={channelType}>
                      <TableCell className="font-medium">{channelTypeLabels[channelType]}</TableCell>
                      <TableCell>{formatNumber(row?.totalInbound ?? 0)}</TableCell>
                      <TableCell>{formatNumber(row?.totalOutbound ?? 0)}</TableCell>
                      <TableCell>{formatNumber(row?.faqReplies ?? 0)}</TableCell>
                      <TableCell>{formatNumber(row?.ruleReplies ?? 0)}</TableCell>
                      <TableCell>{formatNumber(row?.aiReplies ?? 0)}</TableCell>
                      <TableCell>{formatNumber(row?.escalations ?? 0)}</TableCell>
                    </TableRow>
                  )
                })
              )}
            </TableBody>
          </Table>
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-lg font-semibold">Daily Breakdown</h2>
        <div className="overflow-hidden rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Date</TableHead>
                <TableHead>Inbound</TableHead>
                <TableHead>Outbound</TableHead>
                <TableHead>Auto replies</TableHead>
                <TableHead>AI tokens</TableHead>
                <TableHead>Escalations</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {dashboardQuery.isLoading ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-muted-foreground py-6 text-center">
                    Loading daily metrics...
                  </TableCell>
                </TableRow>
              ) : (stats?.dailyBreakdown.length ?? 0) === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-muted-foreground py-6 text-center">
                    No daily activity yet.
                  </TableCell>
                </TableRow>
              ) : (
                stats?.dailyBreakdown
                  .slice()
                  .reverse()
                  .map((row) => (
                    <TableRow key={row.date}>
                      <TableCell className="font-medium">{formatDate(row.date)}</TableCell>
                      <TableCell>{formatNumber(row.totalInbound)}</TableCell>
                      <TableCell>{formatNumber(row.totalOutbound)}</TableCell>
                      <TableCell>{formatNumber(row.faqReplies + row.ruleReplies + row.aiReplies)}</TableCell>
                      <TableCell>{formatNumber(row.aiTokensUsed)}</TableCell>
                      <TableCell>{formatNumber(row.escalations)}</TableCell>
                    </TableRow>
                  ))
              )}
            </TableBody>
          </Table>
        </div>
      </section>
    </div>
  )
}

function KpiCard({
  label,
  value,
  loading,
}: {
  label: string
  value?: number | string
  loading: boolean
}) {
  return (
    <div className="rounded-md border p-4">
      <div className="text-muted-foreground text-xs font-medium uppercase tracking-wide">{label}</div>
      <div className="mt-3 text-2xl font-semibold">
        {loading ? <span className="bg-muted block h-8 w-20 rounded-md" /> : value ?? 0}
      </div>
    </div>
  )
}

function formatNumber(value: number) {
  return new Intl.NumberFormat().format(value)
}

function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString()
}
