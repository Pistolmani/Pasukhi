import api from './client'

export interface LoginRequest {
  email: string
  password: string
}

export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  role: string
  businessId: string | null
}

export interface AuthResponse {
  accessToken: string
  user: User
}

export const authApi = {
  login: (data: LoginRequest) =>
    api.post<AuthResponse>('/api/auth/login', data).then((response) => response.data),
  logout: () => api.post('/api/auth/logout'),
  me: () => api.get<User>('/api/auth/me').then((response) => response.data),
}
