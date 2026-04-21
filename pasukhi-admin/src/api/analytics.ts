import api from './client'
import type { DashboardStats } from '../types/analytics'

export const analyticsApi = {
  getDashboard: (days = 7) =>
    api
      .get<DashboardStats>('/api/analytics/dashboard', { params: { days } })
      .then((r) => r.data),
}
