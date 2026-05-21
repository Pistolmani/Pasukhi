import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { channelsApi } from '../../api/channels'
import { faqsApi } from '../../api/faqs'
import { ChannelType, type ChannelConnection } from '../../types/channel'

interface Props {
  channels: ChannelConnection[]
}

export function MessengerProfilePanel({ channels }: Props) {
  const hasMessenger = channels.some(
    (c) => c.channelType === ChannelType.Messenger && c.isActive,
  )

  const greetingQuery = useQuery({
    queryKey: ['messenger-profile', 'greeting'],
    queryFn: channelsApi.getMessengerGreeting,
    enabled: hasMessenger,
  })

  const faqsQuery = useQuery({
    queryKey: ['faqs'],
    queryFn: faqsApi.list,
    enabled: hasMessenger,
  })

  const [greetingText, setGreetingText] = useState('')

  useEffect(() => {
    if (greetingQuery.data?.greetingText) {
      setGreetingText(greetingQuery.data.greetingText)
    }
  }, [greetingQuery.data])

  const topFaqs = (faqsQuery.data ?? [])
    .filter((f) => f.isActive)
    .slice(0, 4)

  if (!hasMessenger) return null

  return (
    <section className="rounded-md border p-4 space-y-4">
      <div>
        <h2 className="text-lg font-semibold">Messenger Profile</h2>
        <p className="text-muted-foreground text-sm">
          Sync greeting text and ice breakers to your Facebook Page.
        </p>
      </div>
    </section>
  )
}
