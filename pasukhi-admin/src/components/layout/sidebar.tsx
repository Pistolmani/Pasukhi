import { NavLink } from 'react-router-dom'
import { AlertTriangle, BookOpen, Bot, ClipboardCheck, GitBranch, HelpCircle, LayoutDashboard, MessageCircle, Settings2, Webhook } from 'lucide-react'

const navItems = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/channels', label: 'Channels', icon: GitBranch },
  { to: '/conversations', label: 'Conversations', icon: MessageCircle },
  { to: '/escalations', label: 'Escalations', icon: AlertTriangle },
  { to: '/faqs', label: 'FAQs', icon: HelpCircle },
  { to: '/rules', label: 'Rules', icon: BookOpen },
  { to: '/bot-readiness', label: 'Bot Readiness', icon: ClipboardCheck },
  { to: '/ai', label: 'AI Settings', icon: Bot },
  { to: '/settings', label: 'Settings', icon: Settings2 },
  { to: '/webhooks', label: 'Webhooks', icon: Webhook },
]

export function Sidebar() {
  return (
    <aside className="border-border bg-background md:min-h-screen md:w-64 md:border-r">
      <div className="border-border border-b px-4 py-5">
        <div className="text-lg font-semibold">Pasukhi</div>
        <div className="text-muted-foreground text-sm">Operator console</div>
      </div>
      <nav className="flex gap-2 overflow-x-auto p-3 md:flex-col md:overflow-visible">
        {navItems.map((item) => {
          const Icon = item.icon
          return (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) =>
                [
                  'flex min-w-fit items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:bg-muted hover:text-foreground',
                ].join(' ')
              }
            >
              <Icon className="size-4" />
              {item.label}
            </NavLink>
          )
        })}
      </nav>
    </aside>
  )
}
