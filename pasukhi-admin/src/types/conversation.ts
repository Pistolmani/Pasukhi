import type { ChannelType } from './channel'
export { ChannelType, channelTypeLabels } from './channel'

export const ConversationStatus = {
  Active: 0,
  Escalated: 1,
  Resolved: 2,
  Archived: 3,
} as const

export type ConversationStatus = (typeof ConversationStatus)[keyof typeof ConversationStatus]

export const MessageDirection = {
  Inbound: 0,
  Outbound: 1,
} as const

export type MessageDirection = (typeof MessageDirection)[keyof typeof MessageDirection]

export const DeliveryStatus = {
  Pending: 0,
  Sent: 1,
  Delivered: 2,
  Read: 3,
  Failed: 4,
} as const

export type DeliveryStatus = (typeof DeliveryStatus)[keyof typeof DeliveryStatus]

export type ConversationListItem = {
  id: string
  channelType: ChannelType
  externalCustomerId: string
  customerDisplayName?: string | null
  status: ConversationStatus
  isEscalated: boolean
  unreadCount: number
  lastMessageAt?: string | null
  lastMessageSnippet?: string | null
  createdAt: string
  updatedAt: string
}

export type Message = {
  id: string
  direction: MessageDirection
  source: number
  messageType: number
  textContent?: string | null
  mediaUrl?: string | null
  deliveryStatus: DeliveryStatus
  createdAt: string
}

export type ConversationDetail = ConversationListItem & {
  hasMoreMessages: boolean
  nextMessagesCursor?: string | null
  messages: Message[]
}

export const conversationStatusLabels: Record<ConversationStatus, string> = {
  [ConversationStatus.Active]: 'Active',
  [ConversationStatus.Escalated]: 'Escalated',
  [ConversationStatus.Resolved]: 'Resolved',
  [ConversationStatus.Archived]: 'Archived',
}

export const messageDirectionLabels: Record<MessageDirection, string> = {
  [MessageDirection.Inbound]: 'Inbound',
  [MessageDirection.Outbound]: 'Outbound',
}

export const deliveryStatusLabels: Record<DeliveryStatus, string> = {
  [DeliveryStatus.Pending]: 'Pending',
  [DeliveryStatus.Sent]: 'Sent',
  [DeliveryStatus.Delivered]: 'Delivered',
  [DeliveryStatus.Read]: 'Read',
  [DeliveryStatus.Failed]: 'Failed',
}
