import axios from 'axios'
import type { InternalAxiosRequestConfig } from 'axios'
import { useAuthStore } from '../stores/auth-store'

interface RetryConfig extends InternalAxiosRequestConfig {
  _retry?: boolean
}

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000',
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

let refreshing = false

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const originalRequest = error.config as RetryConfig | undefined

    if (
      error.response?.status === 401 &&
      originalRequest &&
      !originalRequest._retry &&
      !refreshing
    ) {
      originalRequest._retry = true
      refreshing = true

      try {
        const { data } = await axios.post(
          `${api.defaults.baseURL}/api/auth/refresh`,
          {},
          { withCredentials: true },
        )

        useAuthStore.getState().setAuth(data.user, data.accessToken)
        originalRequest.headers.Authorization = `Bearer ${data.accessToken}`
        return api(originalRequest)
      } catch {
        useAuthStore.getState().clearAuth()
        window.location.href = '/login'
      } finally {
        refreshing = false
      }
    }

    // Detect plan limit errors (HTTP 402) and surface the upgrade modal.
    if (error.response?.status === 402 && error.response.data?.error === 'plan_limit_exceeded') {
      import('../stores/upgrade-prompt-store').then(({ useUpgradePromptStore }) => {
        useUpgradePromptStore.getState().open(error.response!.data)
      })
    }

    return Promise.reject(error)
  },
)

export default api
