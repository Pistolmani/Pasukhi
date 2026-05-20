import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, ChevronRight, HelpCircle, Loader2, Plus, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { toast } from 'sonner'
import { botReadinessApi } from '../../../api/bot-readiness'
import { faqsApi } from '../../../api/faqs'
import { Button } from '../../../components/ui/button'
import { Input } from '../../../components/ui/input'
import { Label } from '../../../components/ui/label'
import { Textarea } from '../../../components/ui/textarea'

const POLL_INTERVAL_MS = 2000
const POLL_TIMEOUT_MS = 30000

export function SuggestedFaqsStep({ onNext }: { onNext: () => void }) {
  const qc = useQueryClient()
  const [pollAttempts, setPollAttempts] = useState(0)

  const reportQuery = useQuery({
    queryKey: ['bot-readiness', 'report'],
    queryFn: botReadinessApi.getReport,
    refetchInterval: (q) => {
      const data = q.state.data
      const hasFaqs = data?.suggestions?.some((s) => s.type === 'faq' && s.status === 'pending')
      const timedOut = pollAttempts * POLL_INTERVAL_MS >= POLL_TIMEOUT_MS
      return hasFaqs || timedOut ? false : POLL_INTERVAL_MS
    },
  })

  useEffect(() => {
    if (reportQuery.isFetching) setPollAttempts((n) => n + 1)
  }, [reportQuery.isFetching])

  const pendingFaqs =
    reportQuery.data?.suggestions?.filter((s) => s.type === 'faq' && s.status === 'pending') ?? []
  const stillGenerating =
    pendingFaqs.length === 0 && pollAttempts * POLL_INTERVAL_MS < POLL_TIMEOUT_MS

  const approve = async (id: string) => {
    try {
      await botReadinessApi.approveSuggestion(id)
      qc.invalidateQueries({ queryKey: ['bot-readiness', 'report'] })
      toast.success('FAQ added')
    } catch {
      toast.error('Failed to approve')
    }
  }

  const reject = async (id: string) => {
    try {
      await botReadinessApi.rejectSuggestion(id)
      qc.invalidateQueries({ queryKey: ['bot-readiness', 'report'] })
    } catch {
      toast.error('Failed to reject')
    }
  }

  const [showManual, setShowManual] = useState(false)
  const [manualQ, setManualQ] = useState('')
  const [manualA, setManualA] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const addManual = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!manualQ.trim() || !manualA.trim()) return
    setSubmitting(true)
    try {
      await faqsApi.create({
        question: manualQ.trim(),
        answer: manualA.trim(),
        keywords: null,
        isActive: true,
        sortOrder: 0,
      })
      toast.success('FAQ added')
      setManualQ('')
      setManualA('')
      setShowManual(false)
    } catch {
      toast.error('Failed to add FAQ')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div>
      <div className="mb-8">
        <div className="mb-4 flex size-12 items-center justify-center rounded-2xl bg-amber-500/15 text-amber-500">
          <HelpCircle className="size-6" />
        </div>
        <p className="mb-1 text-[12px] font-semibold uppercase tracking-widest text-slate-400">
          Step 5 of 7
        </p>
        <h2 className="text-[26px] font-semibold tracking-tight text-slate-950">
          Review suggested FAQs
        </h2>
        <p className="mt-2 text-[14px] text-slate-500">
          We&apos;ve drafted FAQs based on your answers. Approve the ones you like — add your own at any time.
        </p>
      </div>

      {stillGenerating && (
        <div className="mb-6 flex items-center gap-3 rounded-xl bg-slate-50 px-4 py-4 ring-1 ring-slate-200/60">
          <Loader2 className="size-4 animate-spin text-indigo-500" />
          <span className="text-[13px] text-slate-600">
            Generating suggestions… this usually takes a few seconds.
          </span>
        </div>
      )}

      {pendingFaqs.length > 0 && (
        <div className="mb-6 space-y-3">
          {pendingFaqs.map((s) => (
            <div
              key={s.id}
              className="rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200/60"
            >
              <div className="mb-2 text-[13px] font-semibold text-slate-900">
                {s.payload.question}
              </div>
              <p className="mb-3 text-[13px] leading-relaxed text-slate-600">{s.payload.answer}</p>
              <div className="flex gap-2">
                <Button
                  type="button"
                  size="sm"
                  onClick={() => approve(s.id)}
                  className="gap-1.5"
                >
                  <Check className="size-3.5" />
                  Approve
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  onClick={() => reject(s.id)}
                  className="gap-1.5"
                >
                  <X className="size-3.5" />
                  Reject
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      {!stillGenerating && pendingFaqs.length === 0 && (
        <div className="mb-6 rounded-xl bg-slate-50 px-4 py-4 text-center text-[13px] text-slate-500 ring-1 ring-slate-200/60">
          No AI suggestions this time. You can still add your own FAQs below.
        </div>
      )}

      {showManual ? (
        <form
          onSubmit={addManual}
          className="mb-3 space-y-3 rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200/60"
        >
          <div className="space-y-1.5">
            <Label className="text-[12.5px] font-medium text-slate-700">Question</Label>
            <Input
              autoFocus
              value={manualQ}
              onChange={(e) => setManualQ(e.target.value)}
              placeholder="e.g. Do you ship internationally?"
              className="h-10 border-slate-200 bg-white text-[14px]"
            />
          </div>
          <div className="space-y-1.5">
            <Label className="text-[12.5px] font-medium text-slate-700">Answer</Label>
            <Textarea
              rows={3}
              value={manualA}
              onChange={(e) => setManualA(e.target.value)}
              placeholder="Write the reply the bot should use."
              className="border-slate-200 bg-white text-[14px]"
            />
          </div>
          <div className="flex gap-2">
            <Button type="submit" size="sm" disabled={submitting}>
              {submitting ? <Loader2 className="size-3.5 animate-spin" /> : 'Add FAQ'}
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={() => setShowManual(false)}
            >
              Cancel
            </Button>
          </div>
        </form>
      ) : (
        <button
          type="button"
          onClick={() => setShowManual(true)}
          className="mb-3 flex w-full items-center justify-center gap-2 rounded-xl border border-dashed border-slate-300 px-4 py-3 text-[13px] font-medium text-slate-500 transition-colors hover:border-indigo-400 hover:text-indigo-600"
        >
          <Plus className="size-4" />
          Add your own FAQ
        </button>
      )}

      <Button
        size="lg"
        onClick={onNext}
        className="mt-4 h-12 w-full gap-2 text-[15px] font-medium shadow-[0_6px_18px_-8px_rgba(79,70,229,.55)]"
      >
        Continue
        <ChevronRight className="size-4" />
      </Button>
    </div>
  )
}
