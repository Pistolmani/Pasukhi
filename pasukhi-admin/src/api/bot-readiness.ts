import api from './client'
import type {
  BotKnowledgeSuggestion,
  BotReadinessReport,
  BotReadinessTemplate,
  SaveBotReadinessAnswer,
} from '../types/bot-readiness'

export const botReadinessApi = {
  getTemplate: () =>
    api.get<BotReadinessTemplate>('/api/bot-readiness/template').then((r) => r.data),
  getReport: () =>
    api.get<BotReadinessReport>('/api/bot-readiness/report').then((r) => r.data),
  saveAnswers: (answers: SaveBotReadinessAnswer[]) =>
    api.put<BotReadinessReport>('/api/bot-readiness/answers', { answers }).then((r) => r.data),
  generateSuggestions: () =>
    api.post<BotReadinessReport>('/api/bot-readiness/suggestions/generate').then((r) => r.data),
  approveSuggestion: (id: string) =>
    api.patch<BotKnowledgeSuggestion>(`/api/bot-readiness/suggestions/${id}/approve`).then((r) => r.data),
  rejectSuggestion: (id: string) =>
    api.patch<BotKnowledgeSuggestion>(`/api/bot-readiness/suggestions/${id}/reject`).then((r) => r.data),
}
