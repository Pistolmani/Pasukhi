import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { channelsApi } from '../../api/channels'
import { faqsApi } from '../../api/faqs'
import { Label } from '../../components/ui/label'
import { Textarea } from '../../components/ui/textarea'
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

      <div className="space-y-2">
        <Label htmlFor="greeting">
          Greeting text
          <span className="text-muted-foreground ml-1 text-xs">(max 160 chars)</span>
        </Label>
        <Textarea
          id="greeting"
          maxLength={160}
          value={greetingText}
          onChange={(e) => setGreetingText(e.target.value)}
          placeholder="Hi! How can we help you today?"
          className="resize-none"
          rows={3}
        />
        <p className="text-muted-foreground text-xs text-right">{greetingText.length}/160</p>
      </div>

      <div className="space-y-2">
        <Label>Ice breakers</Label>
        <p className="text-muted-foreground text-xs">
          Your top active FAQs by sort order will appear as clickable buttons in Messenger.
        </p>
        {topFaqs.length === 0 ? (
          <p className="text-muted-foreground text-sm">
            No active FAQs found. Add FAQs on the FAQs page to populate ice breakers.
          </p>
        ) : (
          <ul className="space-y-1">
            {topFaqs.map((faq, i) => (
              <li key={faq.id} className="flex items-start gap-2 text-sm">
                <span className="text-muted-foreground w-4 shrink-0 pt-px">{i + 1}.</span>
                <span className="line-clamp-1">{faq.question}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}
