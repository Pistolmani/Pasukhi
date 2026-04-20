export type BusinessPrompt = {
  id: string
  isAiEnabled: boolean
  systemPrompt: string
  toneDescription: string
  escalationMessage: string
  maxAiTokensPerDay: number
  aiConfidenceThreshold: number
  faqConfidenceThreshold: number
}

export type UpsertBusinessPromptRequest = {
  isAiEnabled: boolean
  systemPrompt: string
  toneDescription: string
  escalationMessage: string
  maxAiTokensPerDay: number
  aiConfidenceThreshold: number
  faqConfidenceThreshold: number
}
