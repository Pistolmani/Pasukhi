export type SubscriptionTier = 'Free' | 'Starter' | 'Pro' | 'Agency' | 'Enterprise'
export type SubscriptionStatus = 'Active' | 'PastDue' | 'Canceled' | 'Incomplete' | 'Trialing'

export interface PlanLimits {
  maxChannels: number
  maxFaqs: number
  maxRules: number
  aiEnabled: boolean
  maxAiTokensPerDay: number
  messengerProfileSync: boolean
  fullAnalytics: boolean
  prioritySupport: boolean
}

export interface BillingStatus {
  tier: SubscriptionTier
  subscriptionStatus: SubscriptionStatus
  currentPeriodEnd: string | null
  hasStripeSubscription: boolean
  limits: PlanLimits
}

export interface PlanLimitError {
  error: 'plan_limit_exceeded'
  resource: string
  limit: number
  currentTier: SubscriptionTier
  suggestedTier: SubscriptionTier
}

export const TIER_PRICES: Record<SubscriptionTier, number | null> = {
  Free: null,
  Starter: 9,
  Pro: 29,
  Agency: 79,
  Enterprise: 199,
}

export const TIER_ORDER: SubscriptionTier[] = ['Free', 'Starter', 'Pro', 'Agency', 'Enterprise']
