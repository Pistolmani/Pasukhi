import api from './client'
import type {
  ChannelConnection,
  SaveChannelConnectionRequest,
  SyncMessengerProfileRequest,
  SyncMessengerProfileResult,
} from '../types/channel'
import { createCrudApi } from './createCrudApi'

export const channelsApi = {
  ...createCrudApi<ChannelConnection, SaveChannelConnectionRequest, SaveChannelConnectionRequest>('/api/channels'),
  syncMessengerProfile: (data: SyncMessengerProfileRequest) =>
    api.post<SyncMessengerProfileResult>('/api/channels/messenger-profile/sync', data).then((r) => r.data),
  getMessengerGreeting: () =>
    api.get<{ greetingText: string | null }>('/api/channels/messenger-profile/greeting').then((r) => r.data),
}
