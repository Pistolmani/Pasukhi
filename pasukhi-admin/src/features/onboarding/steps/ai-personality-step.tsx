import { useQuery } from '@tanstack/react-query'
import { BrainCircuit, ChevronRight, Loader2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { toast } from 'sonner'
import { aiApi } from '../../../api/ai'
import { Button } from '../../../components/ui/button'
import { Label } from '../../../components/ui/label'
import { Textarea } from '../../../components/ui/textarea'

const DEFAULT_TONE = 'Warm, polite, and helpful. Keep replies short and clear.'
const DEFAULT_ESCALATION = "I'll connect you with a teammate who can help with that — one moment."

export function AiPersonalityStep({ onNext }: { onNext: () => void }) {
  const promptQuery = useQuery({ queryKey: ['ai', 'prompt'], queryFn: aiApi.getPrompt })

  const [tone, setTone] = useState(DEFAULT_TONE)
  const [escalation, setEscalation] = useState(DEFAULT_ESCALATION)
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    if (promptQuery.data) {
      if (promptQuery.data.toneDescription) setTone(promptQuery.data.toneDescription)
      if (promptQuery.data.escalationMessage) setEscalation(promptQuery.data.escalationMessage)
    }
  }, [promptQuery.data])

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    try {
      const existing = promptQuery.data
      await aiApi.upsertPrompt({
        isAiEnabled: true,
        systemPrompt: existing?.systemPrompt || '',
        toneDescription: tone,
        escalationMessage: escalation,
        maxAiTokensPerDay: existing?.maxAiTokensPerDay || 50000,
        aiConfidenceThreshold: existing?.aiConfidenceThreshold || 0.6,
        faqConfidenceThreshold: existing?.faqConfidenceThreshold || 0.75,
      })
      onNext()
    } catch {
      toast.error('Failed to save AI personality')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div>
      <div className="mb-8">
        <div className="mb-4 flex size-12 items-center justify-center rounded-2xl bg-violet-500/15 text-violet-500">
          <BrainCircuit className="size-6" />
        </div>
        <p className="mb-1 text-[12px] font-semibold uppercase tracking-widest text-slate-400">
          Step 6 of 7
        </p>
        <h2 className="text-[26px] font-semibold tracking-tight text-slate-950">
          Give your AI a personality
        </h2>
        <p className="mt-2 text-[14px] text-slate-500">
          Tell the bot how to speak and what to say when it needs to hand off to a human.
        </p>
      </div>

      <form onSubmit={onSubmit} className="space-y-5">
        <div className="space-y-1.5">
          <Label className="text-[12.5px] font-medium text-slate-700">Tone</Label>
          <p className="text-[12px] text-slate-400">
            How should the bot sound? Friendly, formal, playful?
          </p>
          <Textarea
            rows={3}
            value={tone}
            onChange={(e) => setTone(e.target.value)}
            className="border-slate-200 bg-white text-[14px]"
          />
        </div>

        <div className="space-y-1.5">
          <Label className="text-[12.5px] font-medium text-slate-700">Escalation message</Label>
          <p className="text-[12px] text-slate-400">
            What the bot says when it hands a conversation to a human.
          </p>
          <Textarea
            rows={3}
            value={escalation}
            onChange={(e) => setEscalation(e.target.value)}
            className="border-slate-200 bg-white text-[14px]"
          />
        </div>

        <Button
          type="submit"
          size="lg"
          disabled={submitting}
          className="mt-2 h-12 w-full gap-2 text-[15px] font-medium shadow-[0_6px_18px_-8px_rgba(79,70,229,.55)]"
        >
          {submitting ? (
            <>
              <Loader2 className="size-4 animate-spin" />
              Saving...
            </>
          ) : (
            <>
              Save personality
              <ChevronRight className="size-4" />
            </>
          )}
        </Button>
      </form>
    </div>
  )
}
