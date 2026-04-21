import api from './client'
import type { BusinessSettings, UpdateBusinessSettingsRequest } from '../types/settings'

export const settingsApi = {
  get: () => api.get<BusinessSettings>('/api/settings').then((r) => r.data),
  update: (data: UpdateBusinessSettingsRequest) =>
    api.put<BusinessSettings>('/api/settings', data).then((r) => r.data),
}
