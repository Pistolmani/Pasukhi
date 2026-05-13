import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, CircleAlert, Sparkles, WandSparkles, XCircle } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { botReadinessApi } from '../../api/bot-readiness'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import { Textarea } from '../../components/ui/textarea'
import type {
  BotKnowledgeSuggestion,
  BotReadinessReport,
  BotReadinessTemplate,
  SaveBotReadinessAnswer,
} from '../../types/bot-readiness'

type DraftAnswer = {
  answerText: string
  isSkipped: boolean
}

function answersFromReport(report?: BotReadinessReport): Record<string, DraftAnswer> {
  return Object.fromEntries(
    (report?.answers ?? []).map((answer) => [
      answer.questionKey,
      {
        answerText: answer.answerText ?? '',
        isSkipped: answer.isSkipped,
      },
    ]),
  )
}

function allQuestionKeys(template?: BotReadinessTemplate): string[] {
  return (template?.sections ?? []).flatMap((section) => section.questions.map((question) => question.key))
}

function suggestionTitle(suggestion: BotKnowledgeSuggestion): string {
  if (suggestion.type === 'faq') return suggestion.payload.question ?? 'FAQ suggestion'
  return 'AI prompt context'
}

function suggestionBody(suggestion: BotKnowledgeSuggestion): string {
  if (suggestion.type === 'faq') return suggestion.payload.answer ?? ''
  return suggestion.payload.context ?? ''
}

function statusClass(status: BotKnowledgeSuggestion['status']): string {
  if (status === 'approved') return 'border-emerald-200 bg-emerald-50 text-emerald-700'
  if (status === 'rejected') return 'border-rose-200 bg-rose-50 text-rose-700'
  return 'border-amber-200 bg-amber-50 text-amber-700'
}

export function BotReadinessPage() {
  const queryClient = useQueryClient()
  const [draftAnswers, setDraftAnswers] = useState<Record<string, DraftAnswer>>({})

  const templateQuery = useQuery({
    queryKey: ['bot-readiness-template'],
    queryFn: botReadinessApi.getTemplate,
  })

  const reportQuery = useQuery({
    queryKey: ['bot-readiness-report'],
    queryFn: botReadinessApi.getReport,
  })

  const savedAnswers = useMemo(() => answersFromReport(reportQuery.data), [reportQuery.data])
  const completionBySection = useMemo(
    () => new Map((reportQuery.data?.sectionCompletion ?? []).map((section) => [section.sectionKey, section])),
    [reportQuery.data?.sectionCompletion],
  )

  const saveMutation = useMutation({
    mutationFn: (payload: SaveBotReadinessAnswer[]) => botReadinessApi.saveAnswers(payload),
    onSuccess: async (data) => {
      queryClient.setQueryData(['bot-readiness-report'], data)
      setDraftAnswers({})
      await queryClient.invalidateQueries({ queryKey: ['bot-readiness-report'] })
      toast.success('Bot readiness answers saved')
    },
    onError: () => toast.error('Could not save answers'),
  })

  const generateMutation = useMutation({
    mutationFn: botReadinessApi.generateSuggestions,
    onSuccess: async (data) => {
      queryClient.setQueryData(['bot-readiness-report'], data)
      await queryClient.invalidateQueries({ queryKey: ['bot-readiness-report'] })
      toast.success('Suggestions generated')
    },
    onError: () => toast.error('Could not generate suggestions'),
  })

  const approveMutation = useMutation({
    mutationFn: botReadinessApi.approveSuggestion,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['bot-readiness-report'] }),
        queryClient.invalidateQueries({ queryKey: ['faqs'] }),
        queryClient.invalidateQueries({ queryKey: ['ai-prompt'] }),
      ])
      toast.success('Suggestion approved')
    },
    onError: () => toast.error('Could not approve suggestion'),
  })

  const rejectMutation = useMutation({
    mutationFn: botReadinessApi.rejectSuggestion,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['bot-readiness-report'] })
      toast.success('Suggestion rejected')
    },
    onError: () => toast.error('Could not reject suggestion'),
  })

  const setAnswer = (questionKey: string, patch: Partial<DraftAnswer>) => {
    setDraftAnswers((prev) => ({
      ...prev,
      [questionKey]: {
        answerText: prev[questionKey]?.answerText ?? savedAnswers[questionKey]?.answerText ?? '',
        isSkipped: prev[questionKey]?.isSkipped ?? savedAnswers[questionKey]?.isSkipped ?? false,
        ...patch,
      },
    }))
  }

  const saveAnswers = () => {
    const payload = allQuestionKeys(templateQuery.data).map((questionKey) => ({
      questionKey,
      answerText: (draftAnswers[questionKey] ?? savedAnswers[questionKey])?.isSkipped
        ? null
        : ((draftAnswers[questionKey] ?? savedAnswers[questionKey])?.answerText.trim() || null),
      isSkipped: (draftAnswers[questionKey] ?? savedAnswers[questionKey])?.isSkipped ?? false,
    }))

    saveMutation.mutate(payload)
  }

  const report = reportQuery.data
  const pendingSuggestions = report?.suggestions.filter((suggestion) => suggestion.status === 'pending') ?? []
  const readiness = report?.readinessScore ?? 0

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-[26px] font-semibold tracking-tight text-slate-950">Bot Readiness</h2>
          <p className="mt-1 text-[13.5px] text-slate-500">
            Prepare safe business knowledge before connecting Instagram or Messenger automation.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="button" variant="outline" onClick={saveAnswers} disabled={saveMutation.isPending || templateQuery.isLoading}>
            {saveMutation.isPending ? 'Saving...' : 'Save answers'}
          </Button>
          <Button
            type="button"
            onClick={() => generateMutation.mutate()}
            disabled={generateMutation.isPending || reportQuery.isLoading}
          >
            <WandSparkles className="size-4" />
            {generateMutation.isPending ? 'Generating...' : 'Generate suggestions'}
          </Button>
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-[20rem_1fr]">
        <aside className="space-y-4">
          <div className="card-shadow rounded-2xl border border-border bg-white p-5">
            <div className="flex flex-col items-center py-2">
              <ProgressRing value={readiness} />
              <div className="mt-4 text-center text-[12.5px] text-slate-500">
                {report?.answeredWeight ?? 0} of {report?.totalWeight ?? 0} weighted points complete
              </div>
            </div>
          </div>

          <div className="card-shadow rounded-2xl border border-border bg-white p-5">
            <div className="flex items-center gap-2 text-[13px] font-semibold text-slate-950">
              <CircleAlert className="size-4 text-amber-600" />
              Missing info
            </div>
            <div className="mt-3 space-y-3">
              {reportQuery.isLoading && <p className="text-[13px] text-slate-500">Loading gaps...</p>}
              {report && report.gaps.length === 0 && (
                <p className="text-[13px] text-slate-500">Required answers are complete.</p>
              )}
              {report?.gaps.slice(0, 8).map((gap) => (
                <div key={gap.questionKey} className="rounded-xl bg-amber-50/70 p-3">
                  <div className="text-[13px] font-medium text-slate-900">{gap.label}</div>
                  <div className="mt-0.5 text-[11.5px] text-amber-700">{gap.sectionLabel}</div>
                </div>
              ))}
            </div>
          </div>

          <div className="card-shadow rounded-2xl border border-border bg-white p-5">
            <div className="text-[13px] font-semibold text-slate-950">Section completion</div>
            <div className="mt-3 space-y-3">
              {report?.sectionCompletion.map((section) => (
                <div key={section.sectionKey}>
                  <div className="flex items-center justify-between gap-2 text-[12.5px]">
                    <span className="font-medium text-slate-700">{section.label}</span>
                    <span className="tabular-nums text-slate-500">{section.score}%</span>
                  </div>
                  <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-muted">
                    <div className="h-full rounded-full bg-primary" style={{ width: `${section.score}%` }} />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </aside>

        <main className="space-y-4">
          {templateQuery.data?.sections.map((section) => {
            const completion = completionBySection.get(section.key)
            return (
              <section key={section.key} className="card-shadow rounded-2xl border border-border bg-white p-5">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <h3 className="text-[15px] font-semibold text-slate-950">{section.label}</h3>
                    <p className="mt-1 text-[13px] text-slate-500">{section.description}</p>
                  </div>
                  {completion && (
                    <span className="rounded-full bg-indigo-50 px-2.5 py-1 text-[11px] font-medium text-indigo-700">
                      {completion.score}% complete
                    </span>
                  )}
                </div>

                <div className="mt-5 grid gap-4">
                  {section.questions.map((question) => {
                    const draft = draftAnswers[question.key] ?? savedAnswers[question.key] ?? { answerText: '', isSkipped: false }
                    const inputId = `bot-readiness-${question.key}`
                    return (
                      <div key={question.key} className="rounded-xl border border-border/80 bg-stone-50/50 p-4">
                        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                          <Label htmlFor={inputId} className="text-[13px] font-semibold text-slate-800">
                            {question.label}
                            {question.required && <span className="text-rose-600"> *</span>}
                          </Label>
                          <label className="flex items-center gap-2 text-[12px] text-slate-500">
                            <input
                              type="checkbox"
                              className="size-4 rounded border-border"
                              checked={draft.isSkipped}
                              onChange={(event) => setAnswer(question.key, { isSkipped: event.target.checked })}
                            />
                            Skip for now
                          </label>
                        </div>
                        <div className="mt-3">
                          {question.inputType === 'text' ? (
                            <Input
                              id={inputId}
                              value={draft.answerText}
                              disabled={draft.isSkipped}
                              onChange={(event) => setAnswer(question.key, { answerText: event.target.value })}
                            />
                          ) : (
                            <Textarea
                              id={inputId}
                              className="min-h-24"
                              value={draft.answerText}
                              disabled={draft.isSkipped}
                              onChange={(event) => setAnswer(question.key, { answerText: event.target.value })}
                            />
                          )}
                        </div>
                        <p className="mt-2 text-[12px] text-slate-500">{question.helpText}</p>
                      </div>
                    )
                  })}
                </div>
              </section>
            )
          })}

          <section className="card-shadow rounded-2xl border border-border bg-white p-5">
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h3 className="text-[15px] font-semibold text-slate-950">Knowledge suggestions</h3>
                <p className="mt-1 text-[13px] text-slate-500">
                  Approve suggestions to create FAQs or update the managed AI prompt context.
                </p>
              </div>
              <span className="rounded-full bg-amber-50 px-2.5 py-1 text-[11px] font-medium text-amber-700">
                {pendingSuggestions.length} pending
              </span>
            </div>

            <div className="mt-4 space-y-3">
              {report?.suggestions.length === 0 && (
                <p className="text-[13px] text-slate-500">No suggestions generated yet.</p>
              )}
              {report?.suggestions.map((suggestion) => (
                <div key={suggestion.id} className="rounded-xl border border-border bg-stone-50/50 p-4">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0 space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-semibold text-slate-950">{suggestionTitle(suggestion)}</span>
                        <span className={`rounded-full border px-2 py-0.5 text-[11px] font-medium ${statusClass(suggestion.status)}`}>
                          {suggestion.status}
                        </span>
                      </div>
                      <p className="whitespace-pre-wrap text-[13px] text-slate-700">{suggestionBody(suggestion)}</p>
                      {suggestion.payload.keywords && (
                        <p className="text-[12px] text-slate-500">Keywords: {suggestion.payload.keywords}</p>
                      )}
                    </div>
                    <div className="flex shrink-0 gap-2">
                      <Button
                        type="button"
                        variant="outline"
                        disabled={suggestion.status !== 'pending' || approveMutation.isPending}
                        onClick={() => approveMutation.mutate(suggestion.id)}
                      >
                        <CheckCircle2 className="size-4" />
                        Approve
                      </Button>
                      <Button
                        type="button"
                        variant="destructive"
                        disabled={suggestion.status !== 'pending' || rejectMutation.isPending}
                        onClick={() => rejectMutation.mutate(suggestion.id)}
                      >
                        <XCircle className="size-4" />
                        Reject
                      </Button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </section>
        </main>
      </div>
    </div>
  )
}

function ProgressRing({ value }: { value: number }) {
  const size = 168
  const stroke = 12
  const radius = (size - stroke) / 2
  const circumference = 2 * Math.PI * radius
  const dash = circumference * (value / 100)

  return (
    <div className="relative flex items-center justify-center" style={{ width: size, height: size }}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        <defs>
          <linearGradient id="botReadinessGradient" x1="0" y1="0" x2="1" y2="1">
            <stop offset="0%" stopColor="var(--primary)" />
            <stop offset="100%" stopColor="var(--pa-accent)" />
          </linearGradient>
        </defs>
        <circle cx={size / 2} cy={size / 2} r={radius} stroke="#eef2ff" strokeWidth={stroke} fill="none" />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke="url(#botReadinessGradient)"
          strokeWidth={stroke}
          strokeLinecap="round"
          fill="none"
          strokeDasharray={`${dash} ${circumference}`}
          transform={`rotate(-90 ${size / 2} ${size / 2})`}
        />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <Sparkles className="mb-1 size-4 text-amber-500" />
        <div className="text-[42px] font-semibold tracking-tight text-slate-950 tabular-nums">
          {value}
          <span className="ml-0.5 align-top text-[17px] text-slate-400">%</span>
        </div>
        <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-500">Bot readiness</div>
      </div>
    </div>
  )
}
