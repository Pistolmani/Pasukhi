import api from './client'
import type { FaqItem, SaveFaqItemRequest } from '../types/faq'

export const faqsApi = {
  list: () =>
    api.get<FaqItem[]>('/api/faqs').then((response) => response.data),
  getById: (id: string) =>
    api.get<FaqItem>(`/api/faqs/${id}`).then((response) => response.data),
  create: (data: SaveFaqItemRequest) =>
    api.post<FaqItem>('/api/faqs', data).then((response) => response.data),
  update: (id: string, data: SaveFaqItemRequest) =>
    api.put<FaqItem>(`/api/faqs/${id}`, data).then((response) => response.data),
  remove: (id: string) => api.delete(`/api/faqs/${id}`),
}
