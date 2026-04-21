import type { ChannelType } from './channel'

export type ChannelBreakdown = {
  channelType: ChannelType
  totalInbound: number
  totalOutbound: number
  faqReplies: number
  ruleReplies: number
  aiReplies: number
  escalations: number
}

export type DailyBreakdown = {
  date: string
  totalInbound: number
  totalOutbound: number
  faqReplies: number
  ruleReplies: number
  aiReplies: number
  aiTokensUsed: number
  escalations: number
}

export type DashboardStats = {
  totalInbound: number
  totalOutbound: number
  faqReplies: number
  ruleReplies: number
  aiReplies: number
  aiTokensUsed: number
  escalations: number
  autoReplyRate: number
  channelBreakdown: ChannelBreakdown[]
  dailyBreakdown: DailyBreakdown[]
}
