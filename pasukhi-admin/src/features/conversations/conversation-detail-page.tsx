import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { useEffect, useRef, useState } from 'react'
import { ArrowLeft } from 'lucide-react'
import { useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { conversationsApi } from '../../api/conversations'
import { Button } from '../../components/ui/button'
import { Textarea } from '../../components/ui/textarea'
import {
  channelTypeLabels,
  conversationStatusLabels,
  deliveryStatusLabels,
  MessageDirection,
  messageDirectionLabels,
  type Message,
} from '../../types/conversation'

export function ConversationDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [reply, setReply] = useState('')
  const threadEndRef = useRef<HTMLDivElement | null>(null)

  const conversationQuery = useInfiniteQuery({
    queryKey: ['conversations', id],
    queryFn: ({ pageParam }) => conversationsApi.getById(id ?? '', pageParam),
    enabled: Boolean(id),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) =>
      lastPage.hasMoreMessages && lastPage.nextMessagesCursor
        ? lastPage.nextMessagesCursor
        : undefined,
    refetchInterval: 5000,
  })

  const sendMutation = useMutation({
    mutationFn: (text: string) => conversationsApi.sendReply(id ?? '', text),
    onSuccess: async () => {
      setReply('')
      await queryClient.invalidateQueries({ queryKey: ['conversations'] })
      await queryClient.invalidateQueries({ queryKey: ['conversations', id] })
      toast.success('Reply queued')
    },
    onError: () => toast.error('Could not send reply'),
  })

  useEffect(() => {
    threadEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [conversationQuery.data?.pages[0]?.messages.length])

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const text = reply.trim()
    if (!text) {
      toast.error('Reply text is required')
      return
    }
    sendMutation.mutate(text)
  }

  const conversation = conversationQuery.data?.pages[0]
  const oldestPage = conversationQuery.data?.pages.at(-1)
  const messages = conversationQuery.data?.pages
    .slice()
    .reverse()
    .flatMap((page) => page.messages) ?? []

  if (conversationQuery.isLoading) {
    return <div className="text-muted-foreground py-8 text-sm">Loading conversation...</div>
  }

  if (!conversation) {
    return (
      <div className="space-y-4">
        <Button type="button" variant="outline" onClick={() => navigate('/conversations')}>
          <ArrowLeft className="size-4" />
          Back
        </Button>
        <div className="text-muted-foreground text-sm">Conversation not found.</div>
      </div>
    )
  }

  return (
    <div className="flex min-h-[calc(100vh-9rem)] flex-col gap-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="space-y-2">
          <Button type="button" variant="outline" onClick={() => navigate('/conversations')}>
            <ArrowLeft className="size-4" />
            Back
          </Button>
          <div>
            <h1 className="break-words text-2xl font-semibold">
              {conversation.customerDisplayName || conversation.externalCustomerId}
            </h1>
            <div className="text-muted-foreground mt-1 text-sm">{conversation.externalCustomerId}</div>
          </div>
        </div>
        <div className="flex flex-wrap gap-2 text-sm">
          <span className="rounded-md border px-2 py-1">{channelTypeLabels[conversation.channelType] ?? 'Unknown'}</span>
          <span className="rounded-md border px-2 py-1">{conversationStatusLabels[conversation.status] ?? 'Unknown'}</span>
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto rounded-md border p-4">
        <div className="space-y-3">
          {oldestPage?.hasMoreMessages && (
            <div className="flex justify-center">
              <Button
                type="button"
                variant="outline"
                disabled={conversationQuery.isFetchingNextPage}
                onClick={() => conversationQuery.fetchNextPage()}
              >
                {conversationQuery.isFetchingNextPage ? 'Loading...' : 'Load older messages'}
              </Button>
            </div>
          )}
          {messages.map((message) => (
            <MessageBubble key={message.id} message={message} />
          ))}
          {messages.length === 0 && (
            <div className="text-muted-foreground py-10 text-center text-sm">No messages yet.</div>
          )}
          <div ref={threadEndRef} />
        </div>
      </div>

      <form className="space-y-3" onSubmit={onSubmit}>
        <Textarea
          className="min-h-24 resize-none"
          value={reply}
          onChange={(event) => setReply(event.target.value)}
          placeholder="Type a manual reply..."
        />
        <div className="flex justify-end">
          <Button type="submit" disabled={sendMutation.isPending}>
            {sendMutation.isPending ? 'Sending...' : 'Send reply'}
          </Button>
        </div>
      </form>
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
        <div className="whitespace-pre-wrap break-words">{message.textContent || message.mediaUrl || 'No message text'}</div>
        <div className={outbound ? 'mt-2 text-xs opacity-80' : 'text-muted-foreground mt-2 text-xs'}>
          {messageDirectionLabels[message.direction] ?? 'Message'} - {deliveryStatusLabels[message.deliveryStatus] ?? 'Unknown'} -{' '}
          {new Date(message.createdAt).toLocaleString()}
        </div>
      </div>
    </div>
  )
}
