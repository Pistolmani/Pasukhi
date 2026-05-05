import { useQuery } from '@tanstack/react-query'
import { Copy, Webhook } from 'lucide-react'
import { toast } from 'sonner'
import { channelsApi } from '../../api/channels'
import { Button } from '../../components/ui/button'
import { ChannelType, channelTypeLabels, type ChannelConnection } from '../../types/channel'

const webhookPaths: Record<ChannelType, string> = {
  [ChannelType.Instagram]: '/api/webhook/instagram',
  [ChannelType.Messenger]: '/api/webhook/messenger',
}

function copy(text: string) {
  navigator.clipboard.writeText(text)
  toast.success('Copied to clipboard')
}

function WebhookCard({ channel, baseUrl }: { channel: ChannelConnection; baseUrl: string }) {
  const path = webhookPaths[channel.channelType] ?? ''
  const fullUrl = `${baseUrl}${path}`
  const label = channelTypeLabels[channel.channelType]

  return (
    <div className="border-border bg-card rounded-lg border p-4">
      <div className="mb-3 flex items-center gap-2">
        <span className="text-base font-semibold">{label}</span>
        {channel.externalAccountName && (
          <span className="text-muted-foreground text-sm">
            - {channel.externalAccountName}
          </span>
        )}
        <span
          className={`ml-auto rounded-full px-2 py-0.5 text-xs ${
            channel.isActive
              ? 'bg-green-100 text-green-800'
              : 'bg-muted text-muted-foreground'
          }`}
        >
          {channel.isActive ? 'Active' : 'Inactive'}
        </span>
      </div>

      <div className="space-y-2 text-sm">
        <div>
          <div className="text-muted-foreground mb-1 text-xs">Callback URL</div>
          <div className="bg-muted flex items-center justify-between gap-2 rounded px-3 py-2">
            <code className="text-xs break-all">{fullUrl}</code>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="shrink-0"
              onClick={() => copy(fullUrl)}
            >
              <Copy className="size-3.5" />
            </Button>
          </div>
        </div>

        <div>
          <div className="text-muted-foreground mb-1 text-xs">Verify Token</div>
          <div className="bg-muted flex items-center justify-between gap-2 rounded px-3 py-2">
            <code className="text-xs break-all">{channel.verifyToken}</code>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="shrink-0"
              onClick={() => copy(channel.verifyToken)}
            >
              <Copy className="size-3.5" />
            </Button>
          </div>
        </div>

        {channel.lastWebhookAt && (
          <div className="text-muted-foreground pt-1 text-xs">
            Last webhook: {new Date(channel.lastWebhookAt).toLocaleString()}
          </div>
        )}
      </div>
    </div>
  )
}

export function WebhooksPage() {
  const channelsQuery = useQuery({
    queryKey: ['channels'],
    queryFn: channelsApi.list,
  })

  const baseUrl =
    import.meta.env.VITE_PUBLIC_WEBHOOK_BASE_URL ||
    import.meta.env.VITE_API_URL ||
    'https://your-ngrok-url.ngrok.io'

  const channels = channelsQuery.data ?? []

  return (
    <div className="space-y-6">
      <div>
        <div className="flex items-center gap-2">
          <Webhook className="size-5" />
          <h1 className="text-2xl font-semibold">Webhook Settings</h1>
        </div>
        <p className="text-muted-foreground mt-1 text-sm">
          Use these details when configuring your Meta App webhooks. The callback URL
          must be reachable from the public internet (use ngrok in development).
        </p>
      </div>

      {channelsQuery.isLoading && (
        <p className="text-muted-foreground text-sm">Loading channels...</p>
      )}

      {!channelsQuery.isLoading && channels.length === 0 && (
        <div className="border-border rounded-lg border border-dashed p-8 text-center">
          <p className="text-muted-foreground text-sm">
            No channels configured yet. Add a channel first to get its webhook details.
          </p>
        </div>
      )}

      <div className="space-y-3">
        {channels.map((ch) => (
          <WebhookCard key={ch.id} channel={ch} baseUrl={baseUrl} />
        ))}
      </div>
    </div>
  )
}
