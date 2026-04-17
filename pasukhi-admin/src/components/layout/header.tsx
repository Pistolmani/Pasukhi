import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { authApi } from '../../api/auth'
import { useAuthStore } from '../../stores/auth-store'
import { Button } from '../ui/button'

export function Header() {
  const navigate = useNavigate()
  const user = useAuthStore((state) => state.user)
  const clearAuth = useAuthStore((state) => state.clearAuth)

  const signOut = async () => {
    try {
      await authApi.logout()
      toast.success('Signed out')
    } finally {
      clearAuth()
      navigate('/login', { replace: true })
    }
  }

  return (
    <header className="border-border flex items-center justify-between border-b bg-background px-4 py-4">
      <div>
        <div className="text-sm font-medium">{user?.firstName} {user?.lastName}</div>
        <div className="text-muted-foreground text-xs">{user?.email}</div>
      </div>
      <Button type="button" variant="outline" onClick={signOut}>
        Sign out
      </Button>
    </header>
  )
}
