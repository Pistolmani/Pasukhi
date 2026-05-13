import { Bell, LogOut, Search } from 'lucide-react'
import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useLocation, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { authApi } from '../../api/auth'
import { escalationsApi } from '../../api/escalations'
import { useAuthStore } from '../../stores/auth-store'
import { Button } from '../ui/button'

const routeTitles: Record<string, { title: string; section: string }> = {
  '/': { title: 'Dashboard', section: 'Workspace' },
  '/businesses': { title: 'Businesses', section: 'Platform' },
  '/channels': { title: 'Channels', section: 'Configuration' },
  '/conversations': { title: 'Conversations', section: 'Workspace' },
  '/escalations': { title: 'Escalations', section: 'Workspace' },
  '/faqs': { title: 'FAQs', section: 'Automation' },
  '/rules': { title: 'Rules', section: 'Automation' },
  '/bot-readiness': { title: 'Bot Readiness', section: 'Automation' },
  '/ai': { title: 'AI Settings', section: 'Automation' },
  '/settings': { title: 'Settings', section: 'Configuration' },
  '/webhooks': { title: 'Webhooks', section: 'Configuration' },
}

export function Header() {
  const location = useLocation()
  const navigate = useNavigate()
  const user = useAuthStore((state) => state.user)
  const clearAuth = useAuthStore((state) => state.clearAuth)
  const isSuperAdmin = user?.role === 'SuperAdmin'
  const hasBusiness = Boolean(user?.businessId)

  const escalationsQuery = useQuery({
    queryKey: ['header', 'escalations'],
    queryFn: () => escalationsApi.list(false),
    enabled: !isSuperAdmin && hasBusiness,
    refetchInterval: 5000,
  })

  const page = useMemo(() => {
    if (isSuperAdmin && location.pathname === '/') return { title: 'Dashboard', section: 'Platform' }
    const exact = routeTitles[location.pathname]
    if (exact) return exact
    if (location.pathname.startsWith('/conversations/')) return { title: 'Conversation Detail', section: 'Workspace' }
    if (location.pathname.startsWith('/escalations/')) return { title: 'Escalation Detail', section: 'Workspace' }
    return { title: 'Pasukhi', section: 'Admin' }
  }, [isSuperAdmin, location.pathname])

  const signOut = async () => {
    try {
      await authApi.logout()
      toast.success('Signed out')
    } finally {
      clearAuth()
      navigate('/login', { replace: true })
    }
  }

  const initials = `${user?.firstName?.[0] ?? ''}${user?.lastName?.[0] ?? ''}`.toUpperCase() || '?'
  const openEscalations = escalationsQuery.data?.length ?? 0

  return (
    <header className="sticky top-0 z-20 border-b border-border/80 bg-background/90 px-4 py-3 backdrop-blur md:px-6">
      <div className="flex items-center justify-between gap-4">
        <div className="min-w-0">
          <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            {page.section}
          </div>
          <h1 className="mt-0.5 truncate text-[21px] font-semibold tracking-tight text-slate-950">{page.title}</h1>
        </div>

        <div className="flex items-center gap-2">
          {!isSuperAdmin && (
          <button className="hidden h-9 w-[220px] items-center gap-2 rounded-lg border border-border bg-white px-3 text-left text-[12.5px] text-slate-500 shadow-sm lg:flex">
            <Search className="size-3.5" />
            Search conversations
            <span className="ml-auto rounded border border-border bg-muted px-1.5 py-0.5 font-mono text-[10px] text-slate-400">
              ⌘K
            </span>
          </button>
          )}
          <button className="relative flex size-9 items-center justify-center rounded-lg border border-border bg-white text-slate-600 shadow-sm">
            <Bell className="size-4" />
            {openEscalations > 0 && (
              <span className="absolute right-2 top-2 size-2 rounded-full bg-amber-400 ring-2 ring-white" />
            )}
          </button>
          <div className="hidden items-center gap-2 rounded-xl border border-border bg-white px-2.5 py-1.5 shadow-sm sm:flex">
            <div className="flex size-7 items-center justify-center rounded-full bg-indigo-50 text-[11px] font-semibold text-indigo-700">
              {initials}
            </div>
            <div className="min-w-0 leading-tight">
              <div className="max-w-32 truncate text-[12px] font-semibold text-slate-900">
                {user?.firstName} {user?.lastName}
              </div>
              <div className="max-w-32 truncate text-[10.5px] text-slate-500">{user?.role}</div>
            </div>
          </div>
          <Button type="button" variant="outline" size="icon" onClick={signOut} aria-label="Sign out">
            <LogOut className="size-4" />
          </Button>
        </div>
      </div>
    </header>
  )
}
