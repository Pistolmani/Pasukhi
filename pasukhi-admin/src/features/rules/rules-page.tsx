import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { useState } from 'react'
import { toast } from 'sonner'
import { rulesApi } from '../../api/rules'
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
import { Textarea } from '../../components/ui/textarea'
import { firstIssue } from '../../lib/form-utils'
import { ruleSchema } from '../../schemas/rule-schemas'
import {
  actionTypeLabels,
  actionTypes,
  triggerTypeLabels,
  triggerTypes,
  type ActionType,
  type AutomationRule,
  type SaveAutomationRuleRequest,
  type TriggerType,
} from '../../types/rule'

const emptyForm: SaveAutomationRuleRequest = {
  name: '',
  priority: 0,
  triggerType: 0,
  triggerValue: '',
  actionType: 0,
  actionValue: '',
  isActive: true,
}

export function RulesPage() {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<AutomationRule | null>(null)
  const [form, setForm] = useState<SaveAutomationRuleRequest>({ ...emptyForm })

  const rulesQuery = useQuery({
    queryKey: ['rules'],
    queryFn: rulesApi.list,
  })

  const saveMutation = useMutation({
    mutationFn: (payload: SaveAutomationRuleRequest) =>
      editing ? rulesApi.update(editing.id, payload) : rulesApi.create(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['rules'] })
      toast.success(editing ? 'Rule updated' : 'Rule created')
      closeDialog()
    },
    onError: () => toast.error('Could not save rule'),
  })

  const deleteMutation = useMutation({
    mutationFn: rulesApi.remove,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['rules'] })
      toast.success('Rule deleted')
    },
    onError: () => toast.error('Could not delete rule'),
  })

  const openCreate = () => {
    setEditing(null)
    setForm({ ...emptyForm })
    setOpen(true)
  }

  const openEdit = (rule: AutomationRule) => {
    setEditing(rule)
    setForm({
      name: rule.name,
      priority: rule.priority,
      triggerType: rule.triggerType,
      triggerValue: rule.triggerValue,
      actionType: rule.actionType,
      actionValue: rule.actionValue,
      isActive: rule.isActive,
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
      name: form.name.trim(),
      triggerValue: form.triggerValue.trim(),
      actionValue: form.actionValue.trim(),
    }

    const parsed = ruleSchema.safeParse(payload)
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
          <h1 className="text-2xl font-semibold">Rules</h1>
          <p className="text-muted-foreground text-sm">Priority-ordered deterministic automation triggers.</p>
        </div>
        <Button type="button" onClick={openCreate}>Add rule</Button>
      </div>

      <div className="overflow-hidden rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Priority</TableHead>
              <TableHead>Name</TableHead>
              <TableHead>Trigger</TableHead>
              <TableHead>Action</TableHead>
              <TableHead>Matches</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rulesQuery.data?.map((rule) => (
              <TableRow key={rule.id}>
                <TableCell>{rule.priority}</TableCell>
                <TableCell className="font-medium">{rule.name}</TableCell>
                <TableCell>
                  <div>{triggerTypeLabels[rule.triggerType]}</div>
                  <div className="text-muted-foreground max-w-xs truncate text-xs">{rule.triggerValue}</div>
                </TableCell>
                <TableCell>
                  <div>{actionTypeLabels[rule.actionType]}</div>
                  <div className="text-muted-foreground max-w-xs truncate text-xs">{rule.actionValue}</div>
                </TableCell>
                <TableCell>{rule.matchCount}</TableCell>
                <TableCell>{rule.isActive ? 'Active' : 'Paused'}</TableCell>
                <TableCell className="space-x-2 text-right">
                  <Button type="button" variant="outline" onClick={() => openEdit(rule)}>Edit</Button>
                  <Button
                    type="button"
                    variant="destructive"
                    disabled={deleteMutation.isPending}
                    onClick={() => deleteMutation.mutate(rule.id)}
                  >
                    Delete
                  </Button>
                </TableCell>
              </TableRow>
            ))}
            {rulesQuery.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} className="text-muted-foreground py-8 text-center">
                  No rules yet.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      <Dialog open={open} onOpenChange={(nextOpen) => (nextOpen ? setOpen(true) : closeDialog())}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit rule' : 'Add rule'}</DialogTitle>
            <DialogDescription>Use lower priority numbers for rules that should run first.</DialogDescription>
          </DialogHeader>
          <form className="space-y-4" onSubmit={onSubmit}>
            <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
              <div className="space-y-2">
                <Label htmlFor="name">Name</Label>
                <Input
                  id="name"
                  value={form.name}
                  onChange={(event) => setForm({ ...form, name: event.target.value })}
                  required
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="priority">Priority</Label>
                <Input
                  id="priority"
                  type="number"
                  min={0}
                  value={form.priority}
                  onChange={(event) => setForm({ ...form, priority: Number(event.target.value) })}
                />
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>Trigger</Label>
                <Select
                  value={String(form.triggerType)}
                  onValueChange={(value) => setForm({ ...form, triggerType: Number(value) as TriggerType })}
                >
                  <SelectTrigger className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {triggerTypes.map((value) => (
                      <SelectItem key={value} value={String(value)}>
                        {triggerTypeLabels[value]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label>Action</Label>
                <Select
                  value={String(form.actionType)}
                  onValueChange={(value) => setForm({ ...form, actionType: Number(value) as ActionType })}
                >
                  <SelectTrigger className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {actionTypes.map((value) => (
                      <SelectItem key={value} value={String(value)}>
                        {actionTypeLabels[value]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="triggerValue">Trigger value</Label>
              <Input
                id="triggerValue"
                value={form.triggerValue}
                onChange={(event) => setForm({ ...form, triggerValue: event.target.value })}
                placeholder="keyword list, regex, Text, or 18:00-09:00"
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="actionValue">Action value</Label>
              <Textarea
                id="actionValue"
                className="min-h-28"
                value={form.actionValue}
                onChange={(event) => setForm({ ...form, actionValue: event.target.value })}
                required
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
                {editing ? 'Save rule' : 'Create rule'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  )
}
