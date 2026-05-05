import type { ChannelType } from './channel'
import type { Message } from './conversation'

export const EscalationReason = {
  NoMatch: 0,
  LowAiConfidence: 1,
  SafetyCheckFailed: 2,
  CustomerRequested: 3,
  OperatorTriggered: 4,
} as const

export type EscalationReason = (typeof EscalationReason)[keyof typeof EscalationReason]

export const escalationReasonLabels: Record<EscalationReason, string> = {
  [EscalationReason.NoMatch]: 'No match',
  [EscalationReason.LowAiConfidence]: 'Low AI confidence',
  [EscalationReason.SafetyCheckFailed]: 'Safety check failed',
  [EscalationReason.CustomerRequested]: 'Customer requested',
  [EscalationReason.OperatorTriggered]: 'Operator triggered',
}

export type EscalationListItem = {
  id: string
  conversationId: string
  reason: EscalationReason
  notes?: string | null
  aiRejectedResponse?: string | null
  isResolved: boolean
  resolvedAt?: string | null
  externalCustomerId: string
  customerDisplayName?: string | null
  channelType: ChannelType
  createdAt: string
}

export type EscalationDetail = EscalationListItem & {
  resolvedByUserId?: string | null
  recentMessages: Message[]
  updatedAt: string
}
