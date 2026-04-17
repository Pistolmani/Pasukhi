export interface FaqItem {
  id: string
  businessId: string
  question: string
  answer: string
  keywords: string | null
  matchCount: number
  isActive: boolean
  sortOrder: number
  createdAt: string
  updatedAt: string
}

export interface SaveFaqItemRequest {
  question: string
  answer: string
  keywords: string | null
  isActive: boolean
  sortOrder: number
}
