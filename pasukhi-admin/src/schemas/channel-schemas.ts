import { z } from 'zod'
import { channelTypes } from '../types/channel'

export const channelSchema = z.object({
  channelType: z.union([z.literal(channelTypes[0]), z.literal(channelTypes[1]), z.literal(channelTypes[2])]),
  externalAccountId: z.string().trim().min(1).max(200),
  externalAccountName: z.string().trim().max(200).nullable(),
  accessToken: z.string().trim().min(1).max(2000),
  verifyToken: z.string().trim().max(200).nullable().optional(),
  isActive: z.boolean(),
})
