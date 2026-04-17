import { Navigate, Route, Routes } from 'react-router-dom'
import { LoginPage } from './features/auth/login-page'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  )
}

export default App
