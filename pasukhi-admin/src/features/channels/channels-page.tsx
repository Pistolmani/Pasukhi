import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { useState } from 'react'
import { toast } from 'sonner'
import { channelsApi } from '../../api/channels'
import { Button } from '../../components/ui/button'
import { Checkbox } from '../../components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../components/ui/dialog'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../components/ui/table'
import { channelSchema } from '../../schemas/channel-schemas'
import { firstIssue, optionalString } from '../../lib/form-utils'
import {
  channelTypeLabels,
  channelTypes,
  type ChannelConnection,
  type ChannelType,
  type SaveChannelConnectionRequest,
} from '../../types/channel'

const emptyForm: SaveChannelConnectionRequest = {
  channelType: 0,
  externalAccountId: '',
  externalAccountName: null,
  accessToken: '',
  verifyToken: null,
  isActive: true,
}

export function ChannelsPage() {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<ChannelConnection | null>(null)
  const [form, setForm] = useState<SaveChannelConnectionRequest>({ ...emptyForm })

  const channelsQuery = useQuery({
    queryKey: ['channels'],
    queryFn: channelsApi.list,
  })

  const saveMutation = useMutation({
    mutationFn: (payload: SaveChannelConnectionRequest) =>
      editing ? channelsApi.update(editing.id, payload) : channelsApi.create(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['channels'] })
      toast.success(editing ? 'Channel updated' : 'Channel created')
      closeDialog()
    },
    onError: () => toast.error('Could not save channel'),
  })

  const deleteMutation = useMutation({
    mutationFn: channelsApi.remove,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['channels'] })
      toast.success('Channel deleted')
    },
    onError: () => toast.error('Could not delete channel'),
  })

  const openCreate = () => {
    setEditing(null)
    setForm({ ...emptyForm })
    setOpen(true)
  }

  const openEdit = (channel: ChannelConnection) => {
    setEditing(channel)
    setForm({
      channelType: channel.channelType,
      externalAccountId: channel.externalAccountId,
      externalAccountName: channel.externalAccountName,
      accessToken: channel.accessToken,
      verifyToken: channel.verifyToken,
      isActive: channel.isActive,
    })
    setOpen(true)
  }

  const closeDialog = () => {
    setOpen(false)
    setEditing(null)
    setForm({ ...emptyForm })
  }

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const payload = {
      ...form,
      externalAccountId: form.externalAccountId.trim(),
      externalAccountName: optionalString(form.externalAccountName),
      accessToken: form.accessToken.trim(),
      verifyToken: optionalString(form.verifyToken),
    }

    const parsed = channelSchema.safeParse(payload)
    if (!parsed.success) {
      toast.error(firstIssue(parsed.error))
      return
    }

    saveMutation.mutate(parsed.data)
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Channels</h1>
          <p className="text-muted-foreground text-sm">Connect the accounts Pasukhi should listen to.</p>
        </div>
        <Button type="button" onClick={openCreate}>Add channel</Button>
      </div>

      <div className="overflow-hidden rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Channel</TableHead>
              <TableHead>Account</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Webhook</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {channelsQuery.data?.map((channel) => (
              <TableRow key={channel.id}>
                <TableCell>{channelTypeLabels[channel.channelType]}</TableCell>
                <TableCell>
                  <div className="font-medium">{channel.externalAccountName || 'Unnamed account'}</div>
                  <div className="text-muted-foreground text-xs">{channel.externalAccountId}</div>
                </TableCell>
                <TableCell>{channel.isActive ? 'Active' : 'Paused'}</TableCell>
                <TableCell>
                  {channel.lastWebhookAt ? new Date(channel.lastWebhookAt).toLocaleString() : 'Never'}
                </TableCell>
                <TableCell className="space-x-2 text-right">
                  <Button type="button" variant="outline" onClick={() => openEdit(channel)}>Edit</Button>
                  <Button
                    type="button"
                    variant="destructive"
                    disabled={deleteMutation.isPending}
                    onClick={() => deleteMutation.mutate(channel.id)}
                  >
                    Delete
                  </Button>
                </TableCell>
              </TableRow>
            ))}
            {channelsQuery.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-muted-foreground py-8 text-center">
                  No channels yet.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      <Dialog open={open} onOpenChange={(nextOpen) => (nextOpen ? setOpen(true) : closeDialog())}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit channel' : 'Add channel'}</DialogTitle>
            <DialogDescription>Use the external account ID from the provider dashboard.</DialogDescription>
          </DialogHeader>
          <form className="space-y-4" onSubmit={onSubmit}>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>Channel</Label>
                <Select
                  value={String(form.channelType)}
                  disabled={Boolean(editing)}
                  onValueChange={(value) => setForm({ ...form, channelType: Number(value) as ChannelType })}
                >
                  <SelectTrigger className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {channelTypes.map((value) => (
                      <SelectItem key={value} value={String(value)}>
                        {channelTypeLabels[value]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="externalAccountName">Account name</Label>
                <Input
                  id="externalAccountName"
                  value={form.externalAccountName ?? ''}
                  onChange={(event) => setForm({ ...form, externalAccountName: event.target.value })}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="externalAccountId">External account ID</Label>
              <Input
                id="externalAccountId"
                value={form.externalAccountId}
                onChange={(event) => setForm({ ...form, externalAccountId: event.target.value })}
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="accessToken">Access token</Label>
              <Input
                id="accessToken"
                type="password"
                value={form.accessToken}
                onChange={(event) => setForm({ ...form, accessToken: event.target.value })}
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="verifyToken">Verify token</Label>
              <Input
                id="verifyToken"
                value={form.verifyToken ?? ''}
                onChange={(event) => setForm({ ...form, verifyToken: event.target.value })}
                placeholder="Generated when left blank"
              />
            </div>

            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={form.isActive}
                onCheckedChange={(checked) => setForm({ ...form, isActive: checked === true })}
              />
              Active
            </label>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={closeDialog}>Cancel</Button>
              <Button type="submit" disabled={saveMutation.isPending}>
                {editing ? 'Save channel' : 'Create channel'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  )
}
