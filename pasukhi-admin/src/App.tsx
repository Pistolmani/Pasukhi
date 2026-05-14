import { lazy, Suspense } from 'react'
import type { ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/layout/app-layout'
import { AuthGuard } from './components/layout/auth-guard'
import { useAuthStore } from './stores/auth-store'

const OnboardingPage = lazy(() =>
  import('./features/onboarding/onboarding-page').then((module) => ({
    default: module.OnboardingPage,
  })),
)
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
const BusinessesPage = lazy(() =>
  import('./features/businesses/businesses-page').then((module) => ({
    default: module.BusinessesPage,
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
const ForgotPasswordPage = lazy(() =>
  import('./features/auth/forgot-password-page').then((module) => ({
    default: module.ForgotPasswordPage,
  })),
)
const PrivacyPage = lazy(() =>
  import('./features/legal/privacy-page').then((module) => ({
    default: module.PrivacyPage,
  })),
)
const TermsPage = lazy(() =>
  import('./features/legal/terms-page').then((module) => ({
    default: module.TermsPage,
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
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/privacy" element={<PrivacyPage />} />
        <Route path="/terms" element={<TermsPage />} />
        <Route element={<AuthGuard />}>
          <Route path="onboarding" element={<OnboardingRoute />} />
          <Route element={<AppLayout />}>
            <Route index element={<DashboardPage />} />
            <Route
              path="businesses"
              element={
                <RoleRoute role="SuperAdmin">
                  <BusinessesPage />
                </RoleRoute>
              }
            />
            <Route
              path="channels"
              element={
                <BusinessRoute>
                  <ChannelsPage />
                </BusinessRoute>
              }
            />
            <Route
              path="conversations"
              element={
                <BusinessRoute>
                  <ConversationsPage />
                </BusinessRoute>
              }
            />
            <Route
              path="conversations/:id"
              element={
                <BusinessRoute>
                  <ConversationDetailPage />
                </BusinessRoute>
              }
            />
            <Route
              path="escalations"
              element={
                <BusinessRoute>
                  <EscalationsPage />
                </BusinessRoute>
              }
            />
            <Route
              path="escalations/:id"
              element={
                <BusinessRoute>
                  <EscalationDetailPage />
                </BusinessRoute>
              }
            />
            <Route
              path="faqs"
              element={
                <BusinessRoute>
                  <FaqsPage />
                </BusinessRoute>
              }
            />
            <Route
              path="rules"
              element={
                <BusinessRoute>
                  <RulesPage />
                </BusinessRoute>
              }
            />
            <Route
              path="bot-readiness"
              element={
                <BusinessRoute>
                  <BotReadinessPage />
                </BusinessRoute>
              }
            />
            <Route
              path="ai"
              element={
                <BusinessRoute>
                  <AiSettingsPage />
                </BusinessRoute>
              }
            />
            <Route
              path="settings"
              element={
                <BusinessRoute>
                  <SettingsPage />
                </BusinessRoute>
              }
            />
            <Route
              path="webhooks"
              element={
                <BusinessRoute>
                  <WebhooksPage />
                </BusinessRoute>
              }
            />
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Suspense>
  )
}

function OnboardingRoute() {
  const user = useAuthStore((state) => state.user)
  if (user?.businessId) return <Navigate to="/" replace />
  if (user?.role === 'SuperAdmin') return <Navigate to="/" replace />
  return <OnboardingPage />
}

function RoleRoute({ role, children }: { role: string; children: ReactNode }) {
  const user = useAuthStore((state) => state.user)
  return user?.role === role ? children : <Navigate to="/" replace />
}

function BusinessRoute({ children }: { children: ReactNode }) {
  const user = useAuthStore((state) => state.user)
  return user?.businessId ? children : <Navigate to="/onboarding" replace />
}

export default App
