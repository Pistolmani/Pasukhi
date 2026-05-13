import { Outlet } from 'react-router-dom'
import { Header } from './header'
import { Sidebar } from './sidebar'

export function AppLayout() {
  return (
    <div className="min-h-screen bg-background text-foreground md:flex">
      <Sidebar />
      <div className="min-w-0 flex-1">
        <Header />
        <main className="mx-auto w-full max-w-6xl px-4 py-6 md:px-6 md:py-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
