import type { ZodError } from 'zod'

export function optionalString(value: string | null | undefined) {
  const trimmed = value?.trim()
  return trimmed ? trimmed : null
}

export function firstIssue(error: ZodError) {
  return error.issues[0]?.message ?? 'Please check the form.'
}
