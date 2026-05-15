import { useQuery } from '@tanstack/react-query'
import { ChevronRight, Loader2, Sparkles } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { botReadinessApi } from '../../../api/bot-readiness'
import { Button } from '../../../components/ui/button'
import { Label } from '../../../components/ui/label'
import { Textarea } from '../../../components/ui/textarea'

const KEY_QUESTIONS = ['business_summary', 'top_products', 'payment_methods', 'return_policy']

export function BusinessQuestionsStep({ onNext }: { onNext: () => void }) {
  const templateQuery = useQuery({
    queryKey: ['bot-readiness', 'template'],
    queryFn: botReadinessApi.getTemplate,
  })

  const [answers, setAnswers] = useState<Record<string, string>>({})
  const [submitting, setSubmitting] = useState(false)

  const questions = useMemo(() => {
    if (!templateQuery.data) return []
    return templateQuery.data.sections
      .flatMap((s) => s.questions)
      .filter((q) => KEY_QUESTIONS.includes(q.key))
      .sort((a, b) => KEY_QUESTIONS.indexOf(a.key) - KEY_QUESTIONS.indexOf(b.key))
  }, [templateQuery.data])

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    try {
      const payload = questions.map((q) => ({
        questionKey: q.key,
        answerText: answers[q.key]?.trim() || null,
        isSkipped: !answers[q.key]?.trim(),
      }))
      await botReadinessApi.saveAnswers(payload)
      botReadinessApi.generateSuggestions().catch(() => {})
      onNext()
    } catch {
      toast.error('Failed to save answers')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div>
      <div className="mb-8">
        <div className="mb-4 flex size-12 items-center justify-center rounded-2xl bg-amber-500/15 text-amber-500">
          <Sparkles className="size-6" />
        </div>
        <p className="mb-1 text-[12px] font-semibold uppercase tracking-widest text-slate-400">
          Step 4 of 7
        </p>
        <h2 className="text-[26px] font-semibold tracking-tight text-slate-950">
          Tell us about your business
        </h2>
        <p className="mt-2 text-[14px] text-slate-500">
          Your answers train the AI and become the foundation of your bot&apos;s suggested FAQs.
        </p>
      </div>

      {templateQuery.isLoading ? (
        <div className="flex h-40 items-center justify-center">
          <Loader2 className="size-5 animate-spin text-slate-400" />
        </div>
      ) : (
        <form onSubmit={onSubmit} className="space-y-5">
          {questions.map((q) => (
            <div key={q.key} className="space-y-1.5">
              <Label className="text-[12.5px] font-medium text-slate-700">{q.label}</Label>
              {q.helpText && <p className="text-[12px] text-slate-400">{q.helpText}</p>}
              <Textarea
                rows={3}
                value={answers[q.key] || ''}
                onChange={(e) => setAnswers({ ...answers, [q.key]: e.target.value })}
                className="border-slate-200 bg-white text-[14px]"
              />
            </div>
          ))}

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
                Generate FAQs with AI
                <ChevronRight className="size-4" />
              </>
            )}
          </Button>
        </form>
      )}
    </div>
  )
}
