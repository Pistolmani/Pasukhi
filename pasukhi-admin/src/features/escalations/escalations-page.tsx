import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { escalationsApi } from '../../api/escalations'
import { Button } from '../../components/ui/button'
import { Label } from '../../components/ui/label'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../components/ui/table'
import { channelTypeLabels } from '../../types/channel'
import { escalationReasonLabels, type EscalationListItem } from '../../types/escalation'

export function EscalationsPage() {
  const navigate = useNavigate()
  const [includeResolved, setIncludeResolved] = useState(false)

  const escalationsQuery = useQuery({
    queryKey: ['escalations', { includeResolved }],
    queryFn: () => escalationsApi.list(includeResolved),
    refetchInterval: 5000,
  })

  const escalations = escalationsQuery.data ?? []

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Escalations</h1>
          <p className="text-muted-foreground text-sm">
            Conversations that need operator attention.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <input
            id="includeResolved"
            type="checkbox"
            checked={includeResolved}
            onChange={(e) => setIncludeResolved(e.target.checked)}
            className="size-4 rounded border"
          />
          <Label htmlFor="includeResolved" className="cursor-pointer text-sm">
            Show resolved
          </Label>
        </div>
      </div>

      <div className="overflow-hidden rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Reason</TableHead>
              <TableHead>Customer</TableHead>
              <TableHead>Channel</TableHead>
              <TableHead>AI attempted reply</TableHead>
              <TableHead>Created</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {escalations.map((e) => (
              <TableRow key={e.id}>
                <TableCell>
                  <ReasonBadge reason={e.reason} />
                </TableCell>
                <TableCell>
                  <div className="font-medium">{customerName(e)}</div>
                  <div className="text-muted-foreground text-xs">{e.externalCustomerId}</div>
                </TableCell>
                <TableCell>{channelTypeLabels[e.channelType] ?? 'Unknown'}</TableCell>
                <TableCell className="max-w-xs">
                  {e.aiRejectedResponse ? (
                    <span className="text-muted-foreground line-clamp-2 text-xs italic">
                      {e.aiRejectedResponse.length > 80
                        ? e.aiRejectedResponse.slice(0, 80) + '…'
                        : e.aiRejectedResponse}
                    </span>
                  ) : (
                    <span className="text-muted-foreground text-xs">—</span>
                  )}
                </TableCell>
                <TableCell className="text-sm">{new Date(e.createdAt).toLocaleString()}</TableCell>
                <TableCell>
                  {e.isResolved ? (
                    <span className="text-muted-foreground text-xs">Resolved</span>
                  ) : (
                    <span className="text-destructive text-xs font-medium">Open</span>
                  )}
                </TableCell>
                <TableCell className="text-right">
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => navigate(`/escalations/${e.id}`)}
                  >
                    View
                  </Button>
                </TableCell>
              </TableRow>
            ))}
            {!escalationsQuery.isLoading && escalations.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} className="text-muted-foreground py-8 text-center">
                  {includeResolved ? 'No escalations found.' : 'No open escalations.'}
                </TableCell>
              </TableRow>
            )}
            {escalationsQuery.isLoading && (
              <TableRow>
                <TableCell colSpan={7} className="text-muted-foreground py-8 text-center">
                  Loading escalations...
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}

function ReasonBadge({ reason }: { reason: number }) {
  const label = escalationReasonLabels[reason as keyof typeof escalationReasonLabels] ?? 'Unknown'
  return (
    <span className="bg-destructive/10 text-destructive rounded-md px-2 py-0.5 text-xs font-medium">
      {label}
    </span>
  )
}

function customerName(e: EscalationListItem) {
  return e.customerDisplayName || e.externalCustomerId
}
