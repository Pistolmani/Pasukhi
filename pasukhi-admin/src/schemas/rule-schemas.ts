import { z } from 'zod'
import { actionTypes, triggerTypes } from '../types/rule'

export const ruleSchema = z.object({
  name: z.string().trim().min(1).max(200),
  priority: z.number().int().min(0),
  triggerType: z.union([
    z.literal(triggerTypes[0]),
    z.literal(triggerTypes[1]),
    z.literal(triggerTypes[2]),
    z.literal(triggerTypes[3]),
  ]),
  triggerValue: z.string().trim().min(1).max(1000),
  actionType: z.union([z.literal(actionTypes[0]), z.literal(actionTypes[1]), z.literal(actionTypes[2])]),
  actionValue: z.string().trim().min(1).max(4000),
  isActive: z.boolean(),
})
