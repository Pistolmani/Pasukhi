export const ChannelType = {
  Instagram: 0,
  Messenger: 1,
  WhatsApp: 2,
} as const

export type ChannelType = (typeof ChannelType)[keyof typeof ChannelType]

export const channelTypeLabels: Record<ChannelType, string> = {
  [ChannelType.Instagram]: 'Instagram',
  [ChannelType.Messenger]: 'Messenger',
  [ChannelType.WhatsApp]: 'WhatsApp',
}

export const channelTypes = [
  ChannelType.Instagram,
  ChannelType.Messenger,
  ChannelType.WhatsApp,
] as const

export interface ChannelConnection {
  id: string
  businessId: string
  channelType: ChannelType
  externalAccountId: string
  externalAccountName: string | null
  accessToken: string
  verifyToken: string
  isActive: boolean
  lastWebhookAt: string | null
  createdAt: string
  updatedAt: string
}

export interface SaveChannelConnectionRequest {
  channelType: ChannelType
  externalAccountId: string
  externalAccountName: string | null
  accessToken: string
  verifyToken?: string | null
  isActive: boolean
}
