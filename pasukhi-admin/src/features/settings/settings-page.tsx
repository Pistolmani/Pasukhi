import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { useEffect, useState } from 'react'
import { toast } from 'sonner'
import { settingsApi } from '../../api/settings'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import type { UpdateBusinessSettingsRequest } from '../../types/settings'

const DEFAULTS: UpdateBusinessSettingsRequest = {
  autoReplyEnabled: true,
  workingHoursEnabled: false,
  workingHoursStart: '09:00',
  workingHoursEnd: '18:00',
  timezone: 'Asia/Tbilisi',
}

export function SettingsPage() {
  const queryClient = useQueryClient()
  const [form, setForm] = useState<UpdateBusinessSettingsRequest>(DEFAULTS)

  const settingsQuery = useQuery({
    queryKey: ['settings'],
    queryFn: settingsApi.get,
  })

  useEffect(() => {
    if (settingsQuery.data) {
      setForm(settingsQuery.data)
    }
  }, [settingsQuery.data])

  const saveMutation = useMutation({
    mutationFn: settingsApi.update,
    onSuccess: async (data) => {
      setForm(data)
      await queryClient.invalidateQueries({ queryKey: ['settings'] })
      toast.success('Settings saved')
    },
    onError: () => toast.error('Failed to save settings'),
  })

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    saveMutation.mutate(form)
  }

  const set = <K extends keyof UpdateBusinessSettingsRequest>(
    key: K,
    value: UpdateBusinessSettingsRequest[K],
  ) => setForm((prev) => ({ ...prev, [key]: value }))

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Settings</h1>
        <p className="text-muted-foreground text-sm">Automation availability and operating hours.</p>
      </div>

      <form onSubmit={onSubmit} className="max-w-2xl space-y-6">
        <div className="flex items-center gap-3">
          <input
            id="autoReplyEnabled"
            type="checkbox"
            checked={form.autoReplyEnabled}
            onChange={(e) => set('autoReplyEnabled', e.target.checked)}
            className="size-4 rounded border"
          />
          <Label htmlFor="autoReplyEnabled" className="cursor-pointer">
            Auto-reply enabled
          </Label>
        </div>

        <div className="flex items-center gap-3">
          <input
            id="workingHoursEnabled"
            type="checkbox"
            checked={form.workingHoursEnabled}
            onChange={(e) => set('workingHoursEnabled', e.target.checked)}
            className="size-4 rounded border"
          />
          <Label htmlFor="workingHoursEnabled" className="cursor-pointer">
            Use working hours
          </Label>
        </div>

        {form.workingHoursEnabled && (
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="workingHoursStart">Start time</Label>
              <Input
                id="workingHoursStart"
                type="time"
                value={form.workingHoursStart}
                onChange={(e) => set('workingHoursStart', e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="workingHoursEnd">End time</Label>
              <Input
                id="workingHoursEnd"
                type="time"
                value={form.workingHoursEnd}
                onChange={(e) => set('workingHoursEnd', e.target.value)}
              />
            </div>
          </div>
        )}

        <div className="space-y-1.5">
          <Label htmlFor="timezone">Timezone</Label>
          <Input
            id="timezone"
            value={form.timezone}
            onChange={(e) => set('timezone', e.target.value)}
            placeholder="Asia/Tbilisi"
          />
        </div>

        <div className="flex justify-end">
          <Button type="submit" disabled={saveMutation.isPending || settingsQuery.isLoading}>
            {saveMutation.isPending ? 'Saving...' : 'Save settings'}
          </Button>
        </div>
      </form>
    </div>
  )
}
