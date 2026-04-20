import api from './client'
import type { ConversationDetail, ConversationListItem } from '../types/conversation'

export const conversationsApi = {
  list: () =>
    api.get<ConversationListItem[]>('/api/conversations').then((response) => response.data),
  getById: (id: string, before?: string) =>
    api
      .get<ConversationDetail>(`/api/conversations/${id}`, {
        params: before ? { before } : undefined,
      })
      .then((response) => response.data),
  sendReply: (id: string, text: string) =>
    api.post(`/api/conversations/${id}/messages`, { textContent: text }).then((response) => response.data),
}
