import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, CircleAlert, WandSparkles, XCircle } from 'lucide-react'
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

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Bot Readiness</h1>
          <p className="text-muted-foreground text-sm">
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

      <div className="grid gap-4 lg:grid-cols-[18rem_1fr]">
        <aside className="space-y-4">
          <div className="rounded-md border p-4">
            <div className="text-muted-foreground text-sm">Readiness score</div>
            <div className="mt-1 text-3xl font-semibold">{report?.readinessScore ?? 0}%</div>
            <div className="bg-muted mt-3 h-2 overflow-hidden rounded-full">
              <div
                className="bg-primary h-full rounded-full transition-all"
                style={{ width: `${report?.readinessScore ?? 0}%` }}
              />
            </div>
            <div className="text-muted-foreground mt-2 text-xs">
              {report?.answeredWeight ?? 0} of {report?.totalWeight ?? 0} weighted points complete
            </div>
          </div>

          <div className="rounded-md border p-4">
            <div className="flex items-center gap-2 font-medium">
              <CircleAlert className="size-4" />
              Missing info
            </div>
            <div className="mt-3 space-y-2">
              {reportQuery.isLoading && <p className="text-muted-foreground text-sm">Loading gaps...</p>}
              {report && report.gaps.length === 0 && (
                <p className="text-muted-foreground text-sm">Required answers are complete.</p>
              )}
              {report?.gaps.slice(0, 8).map((gap) => (
                <div key={gap.questionKey} className="text-sm">
                  <div className="font-medium">{gap.label}</div>
                  <div className="text-muted-foreground text-xs">{gap.sectionLabel}</div>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-md border p-4">
            <div className="font-medium">Section completion</div>
            <div className="mt-3 space-y-2">
              {report?.sectionCompletion.map((section) => (
                <div key={section.sectionKey}>
                  <div className="flex items-center justify-between gap-2 text-sm">
                    <span>{section.label}</span>
                    <span className="text-muted-foreground">{section.score}%</span>
                  </div>
                  <div className="bg-muted mt-1 h-1.5 overflow-hidden rounded-full">
                    <div className="bg-primary h-full rounded-full" style={{ width: `${section.score}%` }} />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </aside>

        <main className="space-y-6">
          <section className="space-y-4">
            {templateQuery.data?.sections.map((section) => {
              const completion = completionBySection.get(section.key)
              return (
                <div key={section.key} className="rounded-md border p-4">
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                      <h2 className="text-lg font-semibold">{section.label}</h2>
                      <p className="text-muted-foreground text-sm">{section.description}</p>
                    </div>
                    {completion && (
                      <div className="text-muted-foreground text-sm">{completion.score}% complete</div>
                    )}
                  </div>

                  <div className="mt-4 grid gap-4">
                    {section.questions.map((question) => {
                      const draft = draftAnswers[question.key] ?? savedAnswers[question.key] ?? { answerText: '', isSkipped: false }
                      const inputId = `bot-readiness-${question.key}`
                      return (
                        <div key={question.key} className="space-y-2">
                          <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                            <Label htmlFor={inputId}>
                              {question.label}
                              {question.required && <span className="text-destructive"> *</span>}
                            </Label>
                            <label className="text-muted-foreground flex items-center gap-2 text-xs">
                              <input
                                type="checkbox"
                                className="size-4 rounded border"
                                checked={draft.isSkipped}
                                onChange={(event) => setAnswer(question.key, { isSkipped: event.target.checked })}
                              />
                              Skip for now
                            </label>
                          </div>
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
                          <p className="text-muted-foreground text-xs">{question.helpText}</p>
                        </div>
                      )
                    })}
                  </div>
                </div>
              )
            })}
          </section>

          <section className="rounded-md border p-4">
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h2 className="text-lg font-semibold">Knowledge suggestions</h2>
                <p className="text-muted-foreground text-sm">
                  Approve suggestions to create FAQs or update the managed AI prompt context.
                </p>
              </div>
              <div className="text-muted-foreground text-sm">{pendingSuggestions.length} pending</div>
            </div>

            <div className="mt-4 space-y-3">
              {report?.suggestions.length === 0 && (
                <p className="text-muted-foreground text-sm">No suggestions generated yet.</p>
              )}
              {report?.suggestions.map((suggestion) => (
                <div key={suggestion.id} className="rounded-md border p-3">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0 space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-medium">{suggestionTitle(suggestion)}</span>
                        <span className={`rounded-full border px-2 py-0.5 text-xs ${statusClass(suggestion.status)}`}>
                          {suggestion.status}
                        </span>
                      </div>
                      <p className="text-sm whitespace-pre-wrap">{suggestionBody(suggestion)}</p>
                      {suggestion.payload.keywords && (
                        <p className="text-muted-foreground text-xs">Keywords: {suggestion.payload.keywords}</p>
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
