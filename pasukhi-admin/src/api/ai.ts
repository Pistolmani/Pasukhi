import api from './client'
import type { BusinessPrompt, UpsertBusinessPromptRequest } from '../types/ai'

export const aiApi = {
  getPrompt: () =>
    api.get<BusinessPrompt>('/api/ai/prompt').then((r) => r.data),
  upsertPrompt: (request: UpsertBusinessPromptRequest) =>
    api.put<BusinessPrompt>('/api/ai/prompt', request).then((r) => r.data),
}
