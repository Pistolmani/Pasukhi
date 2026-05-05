export type BotReadinessQuestion = {
  key: string
  label: string
  helpText: string
  inputType: 'text' | 'textarea'
  required: boolean
  weight: number
}

export type BotReadinessSection = {
  key: string
  label: string
  description: string
  questions: BotReadinessQuestion[]
}

export type BotReadinessTemplate = {
  sections: BotReadinessSection[]
}

export type BotReadinessAnswer = {
  id: string
  businessId: string
  questionKey: string
  answerText: string | null
  isSkipped: boolean
  updatedAt: string | null
}

export type SaveBotReadinessAnswer = {
  questionKey: string
  answerText: string | null
  isSkipped: boolean
}

export type BotReadinessGap = {
  questionKey: string
  label: string
  sectionKey: string
  sectionLabel: string
  weight: number
}

export type BotReadinessSectionCompletion = {
  sectionKey: string
  label: string
  answeredWeight: number
  totalWeight: number
  score: number
}

export type BotKnowledgeSuggestionStatus = 'pending' | 'approved' | 'rejected'
export type BotKnowledgeSuggestionType = 'faq' | 'prompt_context'

export type BotKnowledgeSuggestion = {
  id: string
  businessId: string
  type: BotKnowledgeSuggestionType
  status: BotKnowledgeSuggestionStatus
  sourceQuestionKeys: string[]
  payload: {
    question?: string
    answer?: string
    keywords?: string | null
    context?: string
    values?: Record<string, string>
  }
  createdAt: string | null
  approvedAt: string | null
  rejectedAt: string | null
}

export type BotReadinessReport = {
  readinessScore: number
  answeredWeight: number
  totalWeight: number
  answers: BotReadinessAnswer[]
  gaps: BotReadinessGap[]
  sectionCompletion: BotReadinessSectionCompletion[]
  suggestions: BotKnowledgeSuggestion[]
}
