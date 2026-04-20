import api from './client'
import type { EscalationDetail, EscalationListItem } from '../types/escalation'

export const escalationsApi = {
  list: (includeResolved = false) =>
    api
      .get<EscalationListItem[]>('/api/escalations', { params: { includeResolved } })
      .then((r) => r.data),
  getById: (id: string) =>
    api.get<EscalationDetail>(`/api/escalations/${id}`).then((r) => r.data),
  resolve: (id: string, notes?: string | null) =>
    api.patch(`/api/escalations/${id}/resolve`, { notes }).then((r) => r.data),
}
