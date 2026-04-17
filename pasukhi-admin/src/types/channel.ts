export const channelTypes = [0, 1, 2] as const
export type ChannelType = (typeof channelTypes)[number]

export const channelTypeLabels: Record<ChannelType, string> = {
  0: 'Instagram',
  1: 'Messenger',
  2: 'WhatsApp',
}

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
