export const triggerTypes = [0, 1, 2, 3] as const
export const actionTypes = [0, 1, 2] as const

export type TriggerType = (typeof triggerTypes)[number]
export type ActionType = (typeof actionTypes)[number]

export const triggerTypeLabels: Record<TriggerType, string> = {
  0: 'Keyword',
  1: 'Regex',
  2: 'Message type',
  3: 'Time of day',
}

export const actionTypeLabels: Record<ActionType, string> = {
  0: 'Send reply',
  1: 'Tag conversation',
  2: 'Escalate',
}

export interface AutomationRule {
  id: string
  businessId: string
  name: string
  priority: number
  triggerType: TriggerType
  triggerValue: string
  actionType: ActionType
  actionValue: string
  isActive: boolean
  matchCount: number
  createdAt: string
  updatedAt: string
}

export interface SaveAutomationRuleRequest {
  name: string
  priority: number
  triggerType: TriggerType
  triggerValue: string
  actionType: ActionType
  actionValue: string
  isActive: boolean
}
