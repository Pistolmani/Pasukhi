import api from './client'
import type { AutomationRule, SaveAutomationRuleRequest } from '../types/rule'

export interface UpdateRulePriorityItem {
  id: string
  priority: number
}

export const rulesApi = {
  list: () =>
    api.get<AutomationRule[]>('/api/rules').then((response) => response.data),
  getById: (id: string) =>
    api.get<AutomationRule>(`/api/rules/${id}`).then((response) => response.data),
  create: (data: SaveAutomationRuleRequest) =>
    api.post<AutomationRule>('/api/rules', data).then((response) => response.data),
  update: (id: string, data: SaveAutomationRuleRequest) =>
    api.put<AutomationRule>(`/api/rules/${id}`, data).then((response) => response.data),
  updatePriorities: (items: UpdateRulePriorityItem[]) =>
    api.put('/api/rules/priorities', { items }),
  remove: (id: string) => api.delete(`/api/rules/${id}`),
}
