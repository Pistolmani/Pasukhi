import api from './client'
import type { Business, CreateBusinessRequest } from '../types/business'

export const businessesApi = {
  list: () => api.get<Business[]>('/api/businesses').then((response) => response.data),
  create: (data: CreateBusinessRequest) =>
    api.post<Business>('/api/businesses', data).then((response) => response.data),
}
