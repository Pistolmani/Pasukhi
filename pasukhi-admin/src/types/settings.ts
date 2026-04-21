export type BusinessSettings = {
  autoReplyEnabled: boolean
  workingHoursEnabled: boolean
  workingHoursStart: string
  workingHoursEnd: string
  timezone: string
}

export type UpdateBusinessSettingsRequest = BusinessSettings
