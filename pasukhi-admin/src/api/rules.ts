import api from './client'
import type { AutomationRule, SaveAutomationRuleRequest } from '../types/rule'
import { createCrudApi } from './createCrudApi'

export interface UpdateRulePriorityItem {
  id: string
  priority: number
}

export const rulesApi = {
  ...createCrudApi<AutomationRule, SaveAutomationRuleRequest, SaveAutomationRuleRequest>('/api/rules'),
  updatePriorities: (items: UpdateRulePriorityItem[]) =>
    api.put('/api/rules/priorities', { items }),
}
