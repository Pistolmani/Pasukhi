import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/layout/app-layout'
import { AuthGuard } from './components/layout/auth-guard'
import { AiSettingsPage } from './features/ai/ai-settings-page'
import { LoginPage } from './features/auth/login-page'
import { EscalationDetailPage } from './features/escalations/escalation-detail-page'
import { EscalationsPage } from './features/escalations/escalations-page'
import { ChannelsPage } from './features/channels/channels-page'
import { ConversationDetailPage } from './features/conversations/conversation-detail-page'
import { ConversationsPage } from './features/conversations/conversations-page'
import { DashboardPage } from './features/dashboard/dashboard-page'
import { FaqsPage } from './features/faqs/faqs-page'
import { RulesPage } from './features/rules/rules-page'
import { WebhooksPage } from './features/webhooks/webhooks-page'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<AuthGuard />}>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />
          <Route path="channels" element={<ChannelsPage />} />
          <Route path="conversations" element={<ConversationsPage />} />
          <Route path="conversations/:id" element={<ConversationDetailPage />} />
          <Route path="escalations" element={<EscalationsPage />} />
          <Route path="escalations/:id" element={<EscalationDetailPage />} />
          <Route path="faqs" element={<FaqsPage />} />
          <Route path="rules" element={<RulesPage />} />
          <Route path="ai" element={<AiSettingsPage />} />
          <Route path="webhooks" element={<WebhooksPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
