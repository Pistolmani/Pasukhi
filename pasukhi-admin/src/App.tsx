import { Navigate, Route, Routes } from 'react-router-dom'
import { AuthGuard } from './components/layout/auth-guard'
import { LoginPage } from './features/auth/login-page'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<AuthGuard />}>
        <Route path="/" element={<div className="p-8 text-xl font-semibold">Dashboard coming soon</div>} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
