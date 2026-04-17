import { z } from 'zod'

export const faqSchema = z.object({
  question: z.string().trim().min(1).max(500),
  answer: z.string().trim().min(1).max(4000),
  keywords: z.string().trim().max(1000).nullable(),
  isActive: z.boolean(),
  sortOrder: z.number().int().min(0),
})
