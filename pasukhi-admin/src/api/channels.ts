import api from './client'
import type {
  ChannelConnection,
  SaveChannelConnectionRequest,
  SyncMessengerProfileRequest,
  SyncMessengerProfileResult,
} from '../types/channel'

export const channelsApi = {
  list: () =>
    api.get<ChannelConnection[]>('/api/channels').then((response) => response.data),
  getById: (id: string) =>
    api.get<ChannelConnection>(`/api/channels/${id}`).then((response) => response.data),
  create: (data: SaveChannelConnectionRequest) =>
    api.post<ChannelConnection>('/api/channels', data).then((response) => response.data),
  update: (id: string, data: SaveChannelConnectionRequest) =>
    api.put<ChannelConnection>(`/api/channels/${id}`, data).then((response) => response.data),
  remove: (id: string) => api.delete(`/api/channels/${id}`),
  syncMessengerProfile: (data: SyncMessengerProfileRequest) =>
    api.post<SyncMessengerProfileResult>('/api/channels/messenger-profile/sync', data).then((response) => response.data),
  getMessengerGreeting: () =>
    api.get<{ greetingText: string | null }>('/api/channels/messenger-profile/greeting').then((response) => response.data),
}
