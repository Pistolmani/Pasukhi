import {
  AlertTriangle,
  BookOpen,
  Bot,
  Building2,
  ClipboardCheck,
  GitBranch,
  HelpCircle,
  LayoutDashboard,
  MessageCircle,
  Settings2,
  Webhook,
} from 'lucide-react'
import type { ComponentType } from 'react'
import { useQuery } from '@tanstack/react-query'
import { NavLink } from 'react-router-dom'
import { botReadinessApi } from '../../api/bot-readiness'
import { businessesApi } from '../../api/businesses'
import { channelsApi } from '../../api/channels'
import { conversationsApi } from '../../api/conversations'
import { escalationsApi } from '../../api/escalations'
import { useAuthStore } from '../../stores/auth-store'
import { channelTypeLabels } from '../../types/channel'
import { PasukhiMark } from '../brand/pasukhi-mark'

type NavItem = {
  to: string
  label: string
  icon: ComponentType<{ className?: string }>
  badge?: number
  tone?: 'indigo' | 'rose'
}

type NavGroup = {
  label: string
  items: NavItem[]
}

export function Sidebar() {
  const user = useAuthStore((state) => state.user)
  const isSuperAdmin = user?.role === 'SuperAdmin'
  const hasBusiness = Boolean(user?.businessId)

  const conversationsQuery = useQuery({
    queryKey: ['sidebar', 'conversations'],
    queryFn: conversationsApi.list,
    enabled: !isSuperAdmin && hasBusiness,
    refetchInterval: 5000,
  })
  const escalationsQuery = useQuery({
    queryKey: ['sidebar', 'escalations'],
    queryFn: () => escalationsApi.list(false),
    enabled: !isSuperAdmin && hasBusiness,
    refetchInterval: 5000,
  })
  const channelsQuery = useQuery({
    queryKey: ['sidebar', 'channels'],
    queryFn: channelsApi.list,
    enabled: !isSuperAdmin && hasBusiness,
  })
  const readinessQuery = useQuery({
    queryKey: ['sidebar', 'bot-readiness'],
    queryFn: botReadinessApi.getReport,
    enabled: !isSuperAdmin && hasBusiness,
  })
  const businessesQuery = useQuery({
    queryKey: ['sidebar', 'businesses'],
    queryFn: businessesApi.list,
    enabled: isSuperAdmin,
  })

  const operatorNavGroups: NavGroup[] = [
    {
      label: 'Workspace',
      items: [
        { to: '/', label: 'Dashboard', icon: LayoutDashboard },
        {
          to: '/conversations',
          label: 'Conversations',
          icon: MessageCircle,
          badge: conversationsQuery.data?.reduce((sum, c) => sum + (c.unreadCount ?? 0), 0) ?? 0,
        },
        {
          to: '/escalations',
          label: 'Escalations',
          icon: AlertTriangle,
          badge: escalationsQuery.data?.length ?? 0,
          tone: 'rose',
        },
      ],
    },
    {
      label: 'Automation',
      items: [
        { to: '/faqs', label: 'FAQs', icon: HelpCircle },
        { to: '/rules', label: 'Rules', icon: BookOpen },
        { to: '/ai', label: 'AI Settings', icon: Bot },
        { to: '/bot-readiness', label: 'Bot Readiness', icon: ClipboardCheck },
      ],
    },
    {
      label: 'Configuration',
      items: [
        { to: '/channels', label: 'Channels', icon: GitBranch },
        { to: '/webhooks', label: 'Webhooks', icon: Webhook },
        { to: '/settings', label: 'Settings', icon: Settings2 },
      ],
    },
  ]

  const adminNavGroups: NavGroup[] = [
    {
      label: 'Platform',
      items: [
        { to: '/', label: 'Dashboard', icon: LayoutDashboard },
        {
          to: '/businesses',
          label: 'Businesses',
          icon: Building2,
          badge: businessesQuery.data?.length ?? 0,
        },
      ],
    },
  ]

  const navGroups = isSuperAdmin ? adminNavGroups : operatorNavGroups
  const channelNames = (channelsQuery.data ?? [])
    .filter((channel) => channel.isActive)
    .map((channel) => channelTypeLabels[channel.channelType] ?? channel.externalAccountName)
    .filter(Boolean)
  const businessLabel = isSuperAdmin ? 'Platform admin' : user?.businessName || 'No business assigned'
  const accountMeta = isSuperAdmin
    ? `${businessesQuery.data?.length ?? 0} businesses`
    : channelNames.length > 0
      ? channelNames.join(' + ')
      : 'No channels connected'
  const readinessScore = readinessQuery.data?.readinessScore

  return (
    <aside className="dots sticky top-0 z-30 hidden h-screen w-[244px] shrink-0 flex-col bg-sidebar text-sidebar-foreground md:flex">
      <div className="flex items-center gap-2.5 px-5 pb-4 pt-5">
        <div className="flex size-9 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-400 to-primary text-white">
          <PasukhiMark size={17} />
        </div>
        <div className="min-w-0">
          <div className="text-[15px] font-semibold tracking-tight text-white">Pasukhi</div>
          <div className="text-[10.5px] uppercase tracking-[0.16em] text-slate-400">Answer faster</div>
        </div>
      </div>

      <div className="mx-4 rounded-xl border border-white/10 bg-white/[0.04] p-3">
        <div className="text-[11px] uppercase tracking-[0.14em] text-slate-400">
          {isSuperAdmin ? 'Role' : 'Business'}
        </div>
        <div className="mt-1 truncate text-[13px] font-semibold text-white">{businessLabel}</div>
        <div className="mt-1 truncate text-[11.5px] text-slate-400">{accountMeta}</div>
      </div>

      <nav className="nice-scroll flex-1 overflow-y-auto px-3 py-4">
        {navGroups.map((group) => (
          <div key={group.label} className="mb-5">
            <div className="mb-2 px-2 text-[10.5px] font-semibold uppercase tracking-[0.18em] text-slate-500">
              {group.label}
            </div>
            <div className="space-y-1">
              {group.items.map((item) => {
                const Icon = item.icon
                return (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    end={item.to === '/'}
                    className={({ isActive }) =>
                      [
                        'group flex items-center gap-2.5 rounded-xl px-3 py-2.5 text-[13px] font-medium transition-colors',
                        isActive
                          ? 'bg-indigo-500/15 text-white ring-1 ring-indigo-400/20 shadow-[inset_2px_0_0_var(--primary)]'
                          : 'text-slate-400 hover:bg-white/[0.06] hover:text-slate-100',
                      ].join(' ')
                    }
                  >
                    <Icon className="size-4 shrink-0" />
                    <span className="min-w-0 flex-1 truncate">{item.label}</span>
                    {item.badge !== undefined && (
                      <span
                        className={[
                          'rounded-full px-1.5 py-0.5 text-[10.5px] font-semibold',
                          item.tone === 'rose' ? 'bg-rose-500/15 text-rose-200' : 'bg-indigo-400/15 text-indigo-100',
                        ].join(' ')}
                      >
                        {item.badge}
                      </span>
                    )}
                  </NavLink>
                )
              })}
            </div>
          </div>
        ))}
      </nav>

      {!isSuperAdmin && (
      <div className="m-4 rounded-2xl border border-white/10 bg-white/[0.04] p-4">
        <div className="flex items-center justify-between">
          <div>
            <div className="text-[12px] font-semibold text-white">Bot readiness</div>
            <div className="mt-0.5 text-[11.5px] text-slate-400">
              {readinessScore === undefined ? 'Not measured yet' : readinessScore >= 0.8 ? 'Ready for pilots' : 'Needs setup'}
            </div>
          </div>
          <div className="text-[18px] font-semibold tabular-nums text-white">
            {readinessScore === undefined ? '-' : `${Math.round(readinessScore * 100)}%`}
          </div>
        </div>
        <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-white/10">
          <div
            className="h-full rounded-full bg-gradient-to-r from-primary to-amber-400"
            style={{ width: readinessScore === undefined ? '0%' : `${Math.round(readinessScore * 100)}%` }}
          />
        </div>
      </div>
      )}
    </aside>
  )
}
