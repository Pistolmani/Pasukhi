import { Navigate, Outlet } from 'react-router-dom'
import { useAuthStore } from '../../stores/auth-store'

export function AuthGuard() {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated())
  return isAuthenticated ? <Outlet /> : <Navigate to="/login" replace />
}
