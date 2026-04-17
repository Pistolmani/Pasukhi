export function DashboardPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold">Dashboard</h1>
        <p className="text-muted-foreground text-sm">
          Configure channels, FAQs, and rules before webhooks start sending live conversations.
        </p>
      </div>
      <div className="grid gap-4 md:grid-cols-3">
        <div className="rounded-md border p-4">
          <div className="text-sm font-medium">Channels</div>
          <p className="text-muted-foreground mt-2 text-sm">Connect Instagram, Messenger, and WhatsApp accounts.</p>
        </div>
        <div className="rounded-md border p-4">
          <div className="text-sm font-medium">FAQs</div>
          <p className="text-muted-foreground mt-2 text-sm">Prepare deterministic answers for common questions.</p>
        </div>
        <div className="rounded-md border p-4">
          <div className="text-sm font-medium">Rules</div>
          <p className="text-muted-foreground mt-2 text-sm">Route keyword, regex, message type, and schedule triggers.</p>
        </div>
      </div>
    </div>
  )
}
