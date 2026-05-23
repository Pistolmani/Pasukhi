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
import { useCrudPage } from '../../hooks/useCrudPage'
import { ruleSchema } from '../../schemas/rule-schemas'
import {
  actionTypeLabels,
  actionTypes,
  triggerTypeLabels,
  triggerTypes,
  type ActionType,
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
  const crud = useCrudPage({
    queryKey: ['rules'],
    api: rulesApi,
    schema: ruleSchema,
    emptyForm,
    toForm: (rule) => ({
      name: rule.name,
      priority: rule.priority,
      triggerType: rule.triggerType,
      triggerValue: rule.triggerValue,
      actionType: rule.actionType,
      actionValue: rule.actionValue,
      isActive: rule.isActive,
    }),
    preparePayload: (form) => ({
      ...form,
      name: form.name.trim(),
      triggerValue: form.triggerValue.trim(),
      actionValue: form.actionValue.trim(),
    }),
    entityLabel: 'Rule',
  })

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Rules</h1>
          <p className="text-muted-foreground text-sm">Priority-ordered deterministic automation triggers.</p>
        </div>
        <Button type="button" onClick={crud.openCreate}>Add rule</Button>
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
            {crud.query.data?.map((rule) => (
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
                  <Button type="button" variant="outline" onClick={() => crud.openEdit(rule)}>Edit</Button>
                  <Button
                    type="button"
                    variant="destructive"
                    disabled={crud.deleteMutation.isPending}
                    onClick={() => crud.deleteMutation.mutate(rule.id)}
                  >
                    Delete
                  </Button>
                </TableCell>
              </TableRow>
            ))}
            {crud.query.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} className="text-muted-foreground py-8 text-center">
                  No rules yet.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      <Dialog open={crud.open} onOpenChange={(next) => (next ? crud.setOpen(true) : crud.closeDialog())}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{crud.editing ? 'Edit rule' : 'Add rule'}</DialogTitle>
            <DialogDescription>Use lower priority numbers for rules that should run first.</DialogDescription>
          </DialogHeader>
          <form className="space-y-4" onSubmit={crud.onSubmit}>
            <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
              <div className="space-y-2">
                <Label htmlFor="name">Name</Label>
                <Input
                  id="name"
                  value={crud.form.name}
                  onChange={(e) => crud.setForm({ ...crud.form, name: e.target.value })}
                  required
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="priority">Priority</Label>
                <Input
                  id="priority"
                  type="number"
                  min={0}
                  value={crud.form.priority}
                  onChange={(e) => crud.setForm({ ...crud.form, priority: Number(e.target.value) })}
                />
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>Trigger</Label>
                <Select
                  value={String(crud.form.triggerType)}
                  onValueChange={(value) => crud.setForm({ ...crud.form, triggerType: Number(value) as TriggerType })}
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
                  value={String(crud.form.actionType)}
                  onValueChange={(value) => crud.setForm({ ...crud.form, actionType: Number(value) as ActionType })}
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
                value={crud.form.triggerValue}
                onChange={(e) => crud.setForm({ ...crud.form, triggerValue: e.target.value })}
                placeholder="keyword list, regex, Text, or 18:00-09:00"
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="actionValue">Action value</Label>
              <Textarea
                id="actionValue"
                className="min-h-28"
                value={crud.form.actionValue}
                onChange={(e) => crud.setForm({ ...crud.form, actionValue: e.target.value })}
                required
              />
            </div>

            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={crud.form.isActive}
                onCheckedChange={(checked) => crud.setForm({ ...crud.form, isActive: checked === true })}
              />
              Active
            </label>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={crud.closeDialog}>Cancel</Button>
              <Button type="submit" disabled={crud.saveMutation.isPending}>
                {crud.editing ? 'Save rule' : 'Create rule'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  )
}
