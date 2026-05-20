import { z } from 'zod'
import { channelTypes, type ChannelType } from '../types/channel'

const validChannelTypes = new Set<ChannelType>(channelTypes)

export const channelSchema = z.object({
  channelType: z.number().refine((value): value is ChannelType => validChannelTypes.has(value as ChannelType), {
    message: 'Select a valid channel.',
  }),
  externalAccountId: z.string().trim().min(1).max(200),
  externalAccountName: z.string().trim().max(200).nullable(),
  accessToken: z.string().trim().min(1).max(2000),
  verifyToken: z.string().trim().max(200).nullable().optional(),
  isActive: z.boolean(),
})
