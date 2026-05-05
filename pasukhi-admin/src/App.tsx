import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/layout/app-layout'
import { AuthGuard } from './components/layout/auth-guard'

const AiSettingsPage = lazy(() =>
  import('./features/ai/ai-settings-page').then((module) => ({
    default: module.AiSettingsPage,
  })),
)
const BotReadinessPage = lazy(() =>
  import('./features/bot-readiness/bot-readiness-page').then((module) => ({
    default: module.BotReadinessPage,
  })),
)
const ChannelsPage = lazy(() =>
  import('./features/channels/channels-page').then((module) => ({
    default: module.ChannelsPage,
  })),
)
const ConversationDetailPage = lazy(() =>
  import('./features/conversations/conversation-detail-page').then((module) => ({
    default: module.ConversationDetailPage,
  })),
)
const ConversationsPage = lazy(() =>
  import('./features/conversations/conversations-page').then((module) => ({
    default: module.ConversationsPage,
  })),
)
const DashboardPage = lazy(() =>
  import('./features/dashboard/dashboard-page').then((module) => ({
    default: module.DashboardPage,
  })),
)
const EscalationDetailPage = lazy(() =>
  import('./features/escalations/escalation-detail-page').then((module) => ({
    default: module.EscalationDetailPage,
  })),
)
const EscalationsPage = lazy(() =>
  import('./features/escalations/escalations-page').then((module) => ({
    default: module.EscalationsPage,
  })),
)
const FaqsPage = lazy(() =>
  import('./features/faqs/faqs-page').then((module) => ({
    default: module.FaqsPage,
  })),
)
const LoginPage = lazy(() =>
  import('./features/auth/login-page').then((module) => ({
    default: module.LoginPage,
  })),
)
const RulesPage = lazy(() =>
  import('./features/rules/rules-page').then((module) => ({
    default: module.RulesPage,
  })),
)
const SettingsPage = lazy(() =>
  import('./features/settings/settings-page').then((module) => ({
    default: module.SettingsPage,
  })),
)
const WebhooksPage = lazy(() =>
  import('./features/webhooks/webhooks-page').then((module) => ({
    default: module.WebhooksPage,
  })),
)

function App() {
  return (
    <Suspense fallback={null}>
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
            <Route path="bot-readiness" element={<BotReadinessPage />} />
            <Route path="ai" element={<AiSettingsPage />} />
            <Route path="settings" element={<SettingsPage />} />
            <Route path="webhooks" element={<WebhooksPage />} />
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Suspense>
  )
}

export default App
