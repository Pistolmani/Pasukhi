import api from './client'
import type { CreateAdminUserRequest, ManagedAdminUser } from '../types/admin-user'

export const adminUsersApi = {
  list: (businessId?: string) =>
    api
      .get<ManagedAdminUser[]>('/api/admin-users', {
        params: businessId ? { businessId } : undefined,
      })
      .then((response) => response.data),
  create: (data: CreateAdminUserRequest) =>
    api.post<ManagedAdminUser>('/api/admin-users', data).then((response) => response.data),
}
