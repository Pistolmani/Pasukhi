import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/layout/app-layout'
import { AuthGuard } from './components/layout/auth-guard'
import { LoginPage } from './features/auth/login-page'
import { ChannelsPage } from './features/channels/channels-page'
import { DashboardPage } from './features/dashboard/dashboard-page'
import { FaqsPage } from './features/faqs/faqs-page'
import { RulesPage } from './features/rules/rules-page'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<AuthGuard />}>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />
          <Route path="channels" element={<ChannelsPage />} />
          <Route path="faqs" element={<FaqsPage />} />
          <Route path="rules" element={<RulesPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
