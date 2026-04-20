import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { ArrowLeft } from 'lucide-react'
import { useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { escalationsApi } from '../../api/escalations'
import { Button } from '../../components/ui/button'
import { Textarea } from '../../components/ui/textarea'
import { channelTypeLabels } from '../../types/channel'
import { MessageDirection } from '../../types/conversation'
import { escalationReasonLabels } from '../../types/escalation'
import type { Message } from '../../types/conversation'

export function EscalationDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [notes, setNotes] = useState('')

  const escalationQuery = useQuery({
    queryKey: ['escalations', id],
    queryFn: () => escalationsApi.getById(id ?? ''),
    enabled: Boolean(id),
  })

  const resolveMutation = useMutation({
    mutationFn: () => escalationsApi.resolve(id ?? '', notes || null),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['escalations'] })
      await queryClient.invalidateQueries({ queryKey: ['conversations'] })
      toast.success('Escalation resolved')
      navigate('/escalations')
    },
    onError: () => toast.error('Could not resolve escalation'),
  })

  const escalation = escalationQuery.data

  if (escalationQuery.isLoading) {
    return <div className="text-muted-foreground py-8 text-sm">Loading escalation...</div>
  }

  if (!escalation) {
    return (
      <div className="space-y-4">
        <Button type="button" variant="outline" onClick={() => navigate('/escalations')}>
          <ArrowLeft className="size-4" />
          Back
        </Button>
        <div className="text-muted-foreground text-sm">Escalation not found.</div>
      </div>
    )
  }

  const reasonLabel = escalationReasonLabels[escalation.reason] ?? 'Unknown'
  const customerLabel = escalation.customerDisplayName || escalation.externalCustomerId

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="space-y-2">
          <Button type="button" variant="outline" onClick={() => navigate('/escalations')}>
            <ArrowLeft className="size-4" />
            Back
          </Button>
          <div>
            <h1 className="break-words text-2xl font-semibold">{customerLabel}</h1>
            <div className="text-muted-foreground mt-1 text-sm">{escalation.externalCustomerId}</div>
          </div>
        </div>
        <div className="flex flex-wrap gap-2 text-sm">
          <span className="rounded-md border px-2 py-1">
            {channelTypeLabels[escalation.channelType] ?? 'Unknown'}
          </span>
          <span className="bg-destructive/10 text-destructive rounded-md px-2 py-1 font-medium">
            {reasonLabel}
          </span>
          {escalation.isResolved ? (
            <span className="rounded-md border px-2 py-1 text-green-600">Resolved</span>
          ) : (
            <span className="text-destructive rounded-md border px-2 py-1">Open</span>
          )}
        </div>
      </div>

      {/* AI rejected response */}
      {escalation.aiRejectedResponse && (
        <div className="rounded-md border border-amber-300 bg-amber-50 p-4 dark:border-amber-700 dark:bg-amber-950">
          <div className="mb-1 text-xs font-semibold text-amber-700 dark:text-amber-300">
            AI attempted this reply (rejected)
          </div>
          <p className="text-sm whitespace-pre-wrap text-amber-900 dark:text-amber-100">
            {escalation.aiRejectedResponse}
          </p>
        </div>
      )}

      {/* Recent messages */}
      <div>
        <div className="text-muted-foreground mb-2 text-xs font-medium uppercase tracking-wide">
          Recent conversation ({escalation.recentMessages.length} messages)
        </div>
        <div className="min-h-0 rounded-md border p-4">
          <div className="space-y-3">
            {escalation.recentMessages.map((message) => (
              <MessageBubble key={message.id} message={message} />
            ))}
            {escalation.recentMessages.length === 0 && (
              <div className="text-muted-foreground py-6 text-center text-sm">No messages.</div>
            )}
          </div>
        </div>
      </div>

      {/* Resolve panel / resolved banner */}
      {escalation.isResolved ? (
        <div className="rounded-md border border-green-300 bg-green-50 p-4 text-sm text-green-800 dark:border-green-700 dark:bg-green-950 dark:text-green-200">
          Resolved on {new Date(escalation.resolvedAt!).toLocaleString()}
          {escalation.notes && (
            <p className="mt-1 italic">Notes: {escalation.notes}</p>
          )}
        </div>
      ) : (
        <div className="space-y-3 rounded-md border p-4">
          <div className="text-sm font-medium">Resolve escalation</div>
          <Textarea
            className="min-h-20 resize-none"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="Optional notes (e.g. 'Customer was asking about pricing — sent manual reply')"
          />
          <div className="flex justify-end">
            <Button
              type="button"
              onClick={() => resolveMutation.mutate()}
              disabled={resolveMutation.isPending}
            >
              {resolveMutation.isPending ? 'Resolving...' : 'Mark as resolved'}
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}

function MessageBubble({ message }: { message: Message }) {
  const outbound = message.direction === MessageDirection.Outbound

  return (
    <div className={outbound ? 'flex justify-end' : 'flex justify-start'}>
      <div
        className={[
          'max-w-[min(38rem,85%)] rounded-md border px-3 py-2 text-sm',
          outbound ? 'bg-primary text-primary-foreground' : 'bg-muted text-foreground',
        ].join(' ')}
      >
        <div className="whitespace-pre-wrap break-words">
          {message.textContent || message.mediaUrl || 'No message text'}
        </div>
        <div className={outbound ? 'mt-1 text-xs opacity-70' : 'text-muted-foreground mt-1 text-xs'}>
          {new Date(message.createdAt).toLocaleString()}
        </div>
      </div>
    </div>
  )
}
