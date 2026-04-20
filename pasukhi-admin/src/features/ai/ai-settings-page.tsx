import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { useEffect, useState } from 'react'
import { toast } from 'sonner'
import { aiApi } from '../../api/ai'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import { Textarea } from '../../components/ui/textarea'
import type { UpsertBusinessPromptRequest } from '../../types/ai'

const DEFAULTS: UpsertBusinessPromptRequest = {
  isAiEnabled: false,
  systemPrompt: '',
  toneDescription: 'professional and friendly',
  escalationMessage: 'Let me connect you with our team.',
  maxAiTokensPerDay: 50000,
  aiConfidenceThreshold: 0.7,
  faqConfidenceThreshold: 0.85,
}

export function AiSettingsPage() {
  const queryClient = useQueryClient()
  const [form, setForm] = useState<UpsertBusinessPromptRequest>(DEFAULTS)

  const promptQuery = useQuery({
    queryKey: ['ai-prompt'],
    queryFn: aiApi.getPrompt,
    retry: (count, error: unknown) => {
      if (error && typeof error === 'object' && 'response' in error) {
        const status = (error as { response: { status: number } }).response?.status
        if (status === 404) return false
      }
      return count < 2
    },
  })

  useEffect(() => {
    if (promptQuery.data) {
      setForm({
        isAiEnabled: promptQuery.data.isAiEnabled,
        systemPrompt: promptQuery.data.systemPrompt,
        toneDescription: promptQuery.data.toneDescription,
        escalationMessage: promptQuery.data.escalationMessage,
        maxAiTokensPerDay: promptQuery.data.maxAiTokensPerDay,
        aiConfidenceThreshold: promptQuery.data.aiConfidenceThreshold,
        faqConfidenceThreshold: promptQuery.data.faqConfidenceThreshold,
      })
    }
  }, [promptQuery.data])

  const saveMutation = useMutation({
    mutationFn: aiApi.upsertPrompt,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['ai-prompt'] })
      toast.success('AI settings saved')
    },
    onError: () => toast.error('Failed to save AI settings'),
  })

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    saveMutation.mutate(form)
  }

  const set = <K extends keyof UpsertBusinessPromptRequest>(
    key: K,
    value: UpsertBusinessPromptRequest[K],
  ) => setForm((prev) => ({ ...prev, [key]: value }))

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">AI Settings</h1>
        <p className="text-muted-foreground text-sm">
          Configure the AI fallback that responds when no FAQ or rule matches.
        </p>
      </div>

      <form onSubmit={onSubmit} className="max-w-2xl space-y-6">
        {/* Enable toggle */}
        <div className="flex items-center gap-3">
          <input
            id="isAiEnabled"
            type="checkbox"
            checked={form.isAiEnabled}
            onChange={(e) => set('isAiEnabled', e.target.checked)}
            className="size-4 rounded border"
          />
          <Label htmlFor="isAiEnabled" className="cursor-pointer">
            Enable AI fallback replies
          </Label>
        </div>

        {/* System prompt */}
        <div className="space-y-1.5">
          <Label htmlFor="systemPrompt">System prompt</Label>
          <Textarea
            id="systemPrompt"
            className="min-h-36 font-mono text-sm"
            value={form.systemPrompt}
            onChange={(e) => set('systemPrompt', e.target.value)}
            placeholder="You are a helpful customer service assistant for..."
          />
          <p className="text-muted-foreground text-xs">
            Instructions injected at the top of every AI request. Describe the business context, what the AI can and cannot answer, and any hard rules.
          </p>
        </div>

        {/* Tone */}
        <div className="space-y-1.5">
          <Label htmlFor="toneDescription">Tone description</Label>
          <Input
            id="toneDescription"
            value={form.toneDescription}
            onChange={(e) => set('toneDescription', e.target.value)}
            placeholder="professional and friendly"
          />
        </div>

        {/* Escalation message */}
        <div className="space-y-1.5">
          <Label htmlFor="escalationMessage">Escalation fallback message</Label>
          <Input
            id="escalationMessage"
            value={form.escalationMessage}
            onChange={(e) => set('escalationMessage', e.target.value)}
            placeholder="Let me connect you with our team."
          />
          <p className="text-muted-foreground text-xs">
            Shown to the AI as the fallback message if it chooses to escalate. Keep it natural and brand-appropriate.
          </p>
        </div>

        {/* Thresholds */}
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="aiConfidenceThreshold">
              AI confidence threshold{' '}
              <span className="text-muted-foreground font-normal">
                ({(form.aiConfidenceThreshold * 100).toFixed(0)}%)
              </span>
            </Label>
            <Input
              id="aiConfidenceThreshold"
              type="number"
              min={0}
              max={1}
              step={0.05}
              value={form.aiConfidenceThreshold}
              onChange={(e) => set('aiConfidenceThreshold', parseFloat(e.target.value) || 0)}
            />
            <p className="text-muted-foreground text-xs">
              Replies below this score escalate to a human.
            </p>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="faqConfidenceThreshold">
              FAQ confidence threshold{' '}
              <span className="text-muted-foreground font-normal">
                ({(form.faqConfidenceThreshold * 100).toFixed(0)}%)
              </span>
            </Label>
            <Input
              id="faqConfidenceThreshold"
              type="number"
              min={0}
              max={1}
              step={0.05}
              value={form.faqConfidenceThreshold}
              onChange={(e) => set('faqConfidenceThreshold', parseFloat(e.target.value) || 0)}
            />
            <p className="text-muted-foreground text-xs">
              Minimum score for a FAQ match to auto-reply.
            </p>
          </div>
        </div>

        {/* Token budget */}
        <div className="space-y-1.5">
          <Label htmlFor="maxAiTokensPerDay">Daily token budget</Label>
          <Input
            id="maxAiTokensPerDay"
            type="number"
            min={0}
            step={1000}
            value={form.maxAiTokensPerDay}
            onChange={(e) => set('maxAiTokensPerDay', parseInt(e.target.value, 10) || 0)}
          />
          <p className="text-muted-foreground text-xs">
            Once the daily limit is reached, new messages escalate instead of going to AI.
          </p>
        </div>

        <div className="flex justify-end">
          <Button type="submit" disabled={saveMutation.isPending}>
            {saveMutation.isPending ? 'Saving...' : 'Save settings'}
          </Button>
        </div>
      </form>
    </div>
  )
}
