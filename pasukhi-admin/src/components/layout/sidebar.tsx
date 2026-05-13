import {
  AlertTriangle,
  BookOpen,
  Bot,
  ClipboardCheck,
  GitBranch,
  HelpCircle,
  LayoutDashboard,
  MessageCircle,
  Settings2,
  Webhook,
} from 'lucide-react'
import { NavLink } from 'react-router-dom'
import { PasukhiMark } from '../brand/pasukhi-mark'

const navGroups = [
  {
    label: 'Workspace',
    items: [
      { to: '/', label: 'Dashboard', icon: LayoutDashboard },
      { to: '/conversations', label: 'Conversations', icon: MessageCircle, badge: '12' },
      { to: '/escalations', label: 'Escalations', icon: AlertTriangle, badge: '3', tone: 'rose' },
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

export function Sidebar() {
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
        <div className="text-[11px] uppercase tracking-[0.14em] text-slate-400">Business</div>
        <div className="mt-1 truncate text-[13px] font-semibold text-white">Khinkali House</div>
        <div className="mt-1 text-[11.5px] text-slate-400">Instagram + Messenger</div>
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
                    {item.badge && (
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

      <div className="m-4 rounded-2xl border border-white/10 bg-white/[0.04] p-4">
        <div className="flex items-center justify-between">
          <div>
            <div className="text-[12px] font-semibold text-white">Bot readiness</div>
            <div className="mt-0.5 text-[11.5px] text-slate-400">Ready for pilots</div>
          </div>
          <div className="text-[18px] font-semibold tabular-nums text-white">72%</div>
        </div>
        <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-white/10">
          <div className="h-full w-[72%] rounded-full bg-gradient-to-r from-primary to-amber-400" />
        </div>
      </div>
    </aside>
  )
}
