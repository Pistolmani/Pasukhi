import { useQuery } from '@tanstack/react-query'
import { Bot, MessageCircle, Search } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { conversationsApi } from '../../api/conversations'
import { Button } from '../../components/ui/button'
import {
  channelTypeLabels,
  conversationStatusLabels,
  ConversationStatus,
  type ConversationListItem,
} from '../../types/conversation'

const filters = [
  { label: 'All', value: 'all' },
  { label: 'Open', value: 'open' },
  { label: 'Escalated', value: 'escalated' },
  { label: 'Resolved', value: 'resolved' },
] as const

type FilterValue = (typeof filters)[number]['value']

export function ConversationsPage() {
  const navigate = useNavigate()
  const [filter, setFilter] = useState<FilterValue>('all')
  const conversationsQuery = useQuery({
    queryKey: ['conversations'],
    queryFn: conversationsApi.list,
    refetchInterval: 5000,
  })

  const conversations = conversationsQuery.data ?? []
  const filtered = useMemo(
    () =>
      conversations.filter((conversation) => {
        if (filter === 'all') return true
        if (filter === 'open') return conversation.status === ConversationStatus.Active
        if (filter === 'escalated') return conversation.isEscalated || conversation.status === ConversationStatus.Escalated
        return conversation.status === ConversationStatus.Resolved || conversation.status === ConversationStatus.Archived
      }),
    [conversations, filter],
  )

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-[26px] font-semibold tracking-tight text-slate-950">Conversations</h2>
          <p className="mt-1 text-[13.5px] text-slate-500">
            Review customer threads, bot replies, and escalations in one workspace.
          </p>
        </div>
        <div className="flex h-9 items-center gap-2 rounded-lg border border-border bg-white px-3 text-[12.5px] text-slate-500 shadow-sm">
          <Search className="size-3.5" />
          Search by customer or message
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        {filters.map((item) => (
          <button
            key={item.value}
            type="button"
            onClick={() => setFilter(item.value)}
            className={[
              'rounded-full border px-3 py-1.5 text-[12.5px] font-medium transition-colors',
              filter === item.value
                ? 'border-indigo-200 bg-indigo-50 text-indigo-700'
                : 'border-border bg-white text-slate-600 hover:bg-muted',
            ].join(' ')}
          >
            {item.label}
          </button>
        ))}
      </div>

      <section className="card-shadow overflow-hidden rounded-2xl border border-border bg-white">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-[13px]">
            <thead className="border-b border-border bg-muted/50 text-[11px] uppercase tracking-[0.14em] text-slate-500">
              <tr>
                <th className="px-5 py-3 font-semibold">Customer</th>
                <th className="px-5 py-3 font-semibold">Channel</th>
                <th className="px-5 py-3 font-semibold">Last message</th>
                <th className="px-5 py-3 font-semibold">Unread</th>
                <th className="px-5 py-3 font-semibold">Status</th>
                <th className="px-5 py-3 text-right font-semibold">Action</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((conversation) => (
                <tr key={conversation.id} className="border-b border-border/70 last:border-0 hover:bg-muted/35">
                  <td className="px-5 py-4">
                    <div className="flex items-center gap-3">
                      <Avatar name={customerName(conversation)} />
                      <div className="min-w-0">
                        <div className="truncate font-semibold text-slate-950">{customerName(conversation)}</div>
                        <div className="truncate text-[11.5px] text-slate-500">{conversation.externalCustomerId}</div>
                      </div>
                    </div>
                  </td>
                  <td className="px-5 py-4 text-slate-600">
                    {channelTypeLabels[conversation.channelType] ?? 'Unknown'}
                  </td>
                  <td className="max-w-md px-5 py-4">
                    <div className="line-clamp-2 text-slate-700">{conversation.lastMessageSnippet || 'No message text'}</div>
                    <div className="mt-1 text-[11.5px] text-slate-500">{formatDate(conversation.lastMessageAt)}</div>
                  </td>
                  <td className="px-5 py-4 tabular-nums text-slate-600">{conversation.unreadCount}</td>
                  <td className="px-5 py-4">
                    <StatusBadge conversation={conversation} />
                  </td>
                  <td className="px-5 py-4 text-right">
                    <Button type="button" variant="outline" onClick={() => navigate(`/conversations/${conversation.id}`)}>
                      View
                    </Button>
                  </td>
                </tr>
              ))}
              {!conversationsQuery.isLoading && filtered.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-5 py-10 text-center text-slate-500">
                    No conversations match this filter.
                  </td>
                </tr>
              )}
              {conversationsQuery.isLoading && (
                <tr>
                  <td colSpan={6} className="px-5 py-10 text-center text-slate-500">
                    Loading conversations...
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <div className="card-shadow rounded-2xl border border-border bg-white p-5">
        <div className="flex items-start gap-3">
          <div className="flex size-9 items-center justify-center rounded-xl bg-indigo-50 text-indigo-700">
            <Bot className="size-4" />
          </div>
          <div>
            <div className="text-[13px] font-semibold text-slate-950">Bot handoff policy</div>
            <p className="mt-1 max-w-2xl text-[13px] text-slate-500">
              FAQ and rule replies stay automated. Low-confidence or sensitive messages should move into the escalated queue
              for human review before replying.
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}

function Avatar({ name }: { name: string }) {
  const initials = name
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()

  return (
    <div className="flex size-9 shrink-0 items-center justify-center rounded-full bg-indigo-50 text-[12px] font-semibold text-indigo-700">
      {initials || <MessageCircle className="size-4" />}
    </div>
  )
}

function StatusBadge({ conversation }: { conversation: ConversationListItem }) {
  const isEscalated = conversation.isEscalated || conversation.status === ConversationStatus.Escalated
  const tone = isEscalated
    ? 'bg-rose-50 text-rose-700'
    : conversation.status === ConversationStatus.Active
      ? 'bg-indigo-50 text-indigo-700'
      : 'bg-emerald-50 text-emerald-700'
  const label = isEscalated ? 'Escalated' : conversationStatusLabels[conversation.status] ?? 'Unknown'

  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[11px] font-medium ${tone}`}>
      <span className="size-1.5 rounded-full bg-current" />
      {label}
    </span>
  )
}

function customerName(conversation: ConversationListItem) {
  return conversation.customerDisplayName || conversation.externalCustomerId
}

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleString() : 'Never'
}
