import type { FaqItem, SaveFaqItemRequest } from '../types/faq'
import { createCrudApi } from './createCrudApi'

export const faqsApi = createCrudApi<FaqItem, SaveFaqItemRequest, SaveFaqItemRequest>('/api/faqs')
