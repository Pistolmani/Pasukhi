import { create } from 'zustand'
import type { PlanLimitError } from '../types/billing'

interface UpgradePromptState {
  error: PlanLimitError | null
  open: (error: PlanLimitError) => void
  close: () => void
}

export const useUpgradePromptStore = create<UpgradePromptState>((set) => ({
  error: null,
  open: (error) => set({ error }),
  close: () => set({ error: null }),
}))
