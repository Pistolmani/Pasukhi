import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  role: string
  businessId: string | null
  businessName: string | null
}

interface AuthState {
  user: User | null
  accessToken: string | null
  setAuth: (user: User, token: string) => void
  clearAuth: () => void
  isAuthenticated: () => boolean
  isSuperAdmin: () => boolean
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      accessToken: null,
      setAuth: (user, accessToken) => set({ user, accessToken }),
      clearAuth: () => set({ user: null, accessToken: null }),
      isAuthenticated: () => get().accessToken !== null,
      isSuperAdmin: () => get().user?.role === 'SuperAdmin',
    }),
    {
      name: 'pasukhi-auth',
      partialize: (state) => ({ user: state.user, accessToken: state.accessToken }),
    },
  ),
)
