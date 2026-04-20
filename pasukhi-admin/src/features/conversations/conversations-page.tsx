import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { conversationsApi } from '../../api/conversations'
import { Button } from '../../components/ui/button'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../components/ui/table'
import {
  channelTypeLabels,
  conversationStatusLabels,
  type ConversationListItem,
} from '../../types/conversation'

export function ConversationsPage() {
  const navigate = useNavigate()
  const conversationsQuery = useQuery({
    queryKey: ['conversations'],
    queryFn: conversationsApi.list,
    refetchInterval: 5000,
  })

  const conversations = conversationsQuery.data ?? []

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Conversations</h1>
        <p className="text-muted-foreground text-sm">Review inbound customer threads and send manual replies.</p>
      </div>

      <div className="overflow-hidden rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Channel</TableHead>
              <TableHead>Customer</TableHead>
              <TableHead>Last message</TableHead>
              <TableHead>Unread</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {conversations.map((conversation) => (
              <TableRow key={conversation.id}>
                <TableCell>{channelTypeLabels[conversation.channelType] ?? 'Unknown'}</TableCell>
                <TableCell>
                  <div className="font-medium">{customerName(conversation)}</div>
                  <div className="text-muted-foreground text-xs">{conversation.externalCustomerId}</div>
                </TableCell>
                <TableCell className="max-w-md whitespace-normal">
                  <div className="line-clamp-2">{conversation.lastMessageSnippet || 'No message text'}</div>
                  <div className="text-muted-foreground mt-1 text-xs">{formatDate(conversation.lastMessageAt)}</div>
                </TableCell>
                <TableCell>{conversation.unreadCount}</TableCell>
                <TableCell>{conversationStatusLabels[conversation.status] ?? 'Unknown'}</TableCell>
                <TableCell className="text-right">
                  <Button type="button" variant="outline" onClick={() => navigate(`/conversations/${conversation.id}`)}>
                    View
                  </Button>
                </TableCell>
              </TableRow>
            ))}
            {!conversationsQuery.isLoading && conversations.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} className="text-muted-foreground py-8 text-center">
                  No conversations yet.
                </TableCell>
              </TableRow>
            )}
            {conversationsQuery.isLoading && (
              <TableRow>
                <TableCell colSpan={6} className="text-muted-foreground py-8 text-center">
                  Loading conversations...
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}

function customerName(conversation: ConversationListItem) {
  return conversation.customerDisplayName || conversation.externalCustomerId
}

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleString() : 'Never'
}
