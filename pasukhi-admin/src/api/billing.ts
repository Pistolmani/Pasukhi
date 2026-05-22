import type { BillingStatus } from '../types/billing'
import api from './client'

export const billingApi = {
  createCheckout: (tier: string) =>
    api
      .post<{ url: string }>('/api/billing/checkout', { tier })
      .then((r) => r.data),

  createPortal: () =>
    api.post<{ url: string }>('/api/billing/portal').then((r) => r.data),

  getStatus: () =>
    api.get<BillingStatus>('/api/billing/status').then((r) => r.data),
}
