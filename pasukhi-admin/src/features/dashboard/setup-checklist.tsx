import { useQuery } from '@tanstack/react-query'
import { CheckCircle2, Circle, X } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { aiApi } from '../../api/ai'
import { botReadinessApi } from '../../api/bot-readiness'
import { channelsApi } from '../../api/channels'
import { faqsApi } from '../../api/faqs'

const DISMISSED_KEY = 'pasukhi:setup-checklist-dismissed'

type ChecklistItem = {
  label: string
  description: string
  route: string
  done: boolean
}

export function SetupChecklist() {
  const navigate = useNavigate()
  const [dismissed, setDismissed] = useState(
    () => localStorage.getItem(DISMISSED_KEY) === '1',
  )

  const channelsQuery = useQuery({ queryKey: ['channels'], queryFn: channelsApi.list })
  const faqsQuery = useQuery({ queryKey: ['faqs'], queryFn: faqsApi.list })
  const aiQuery = useQuery({ queryKey: ['ai', 'prompt'], queryFn: aiApi.getPrompt })
  const brQuery = useQuery({
    queryKey: ['bot-readiness', 'report'],
    queryFn: botReadinessApi.getReport,
  })

  const loading =
    channelsQuery.isLoading || faqsQuery.isLoading || aiQuery.isLoading || brQuery.isLoading

  const items: ChecklistItem[] = [
    {
      label: 'Connect a channel',
      description: 'Link Instagram, Messenger, or WhatsApp',
      route: '/channels',
      done: (channelsQuery.data?.length ?? 0) > 0,
    },
    {
      label: 'Add your first FAQ',
      description: 'Teach the bot your most common answers',
      route: '/faqs',
      done: (faqsQuery.data?.length ?? 0) > 0,
    },
    {
      label: 'Configure AI personality',
      description: 'Set the bot tone and escalation message',
      route: '/ai',
      done: !!(aiQuery.data?.systemPrompt || aiQuery.data?.toneDescription),
    },
    {
      label: 'Complete bot readiness',
      description: 'Fill in the remaining training questions',
      route: '/bot-readiness',
      done: (brQuery.data?.readinessScore ?? 0) >= 80,
    },
  ]

  const allDone = items.every((i) => i.done)

  if (dismissed || allDone || loading) return null

  return (
    <div className="relative rounded-xl border border-slate-200/70 bg-white p-5 shadow-sm">
      <button
        onClick={() => {
          localStorage.setItem(DISMISSED_KEY, '1')
          setDismissed(true)
        }}
        className="absolute right-4 top-4 text-slate-400 transition-colors hover:text-slate-700"
        aria-label="Dismiss setup checklist"
      >
        <X className="size-4" />
      </button>

      <p className="mb-1 text-[11px] font-semibold uppercase tracking-widest text-slate-400">
        Setup checklist
      </p>
      <h3 className="mb-4 text-[15px] font-semibold text-slate-900">
        Complete your bot setup
      </h3>

      <div className="space-y-2">
        {items.map((item) => (
          <button
            key={item.label}
            onClick={() => !item.done && navigate(item.route)}
            disabled={item.done}
            className={`flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left transition-colors ${
              item.done
                ? 'opacity-50'
                : 'hover:bg-slate-50 cursor-pointer'
            }`}
          >
            {item.done ? (
              <CheckCircle2 className="size-5 shrink-0 text-emerald-500" />
            ) : (
              <Circle className="size-5 shrink-0 text-slate-300" />
            )}
            <div>
              <div className={`text-[13px] font-medium ${item.done ? 'line-through text-slate-400' : 'text-slate-900'}`}>
                {item.label}
              </div>
              <div className="text-[12px] text-slate-400">{item.description}</div>
            </div>
          </button>
        ))}
      </div>

      <div className="mt-4 h-1.5 w-full overflow-hidden rounded-full bg-slate-100">
        <div
          className="h-full rounded-full bg-primary transition-all duration-500"
          style={{ width: `${(items.filter((i) => i.done).length / items.length) * 100}%` }}
        />
      </div>
      <p className="mt-1.5 text-[12px] text-slate-400">
        {items.filter((i) => i.done).length} of {items.length} complete
      </p>
    </div>
  )
}
