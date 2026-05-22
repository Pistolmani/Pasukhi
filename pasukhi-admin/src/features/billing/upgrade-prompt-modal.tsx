import { useMutation } from '@tanstack/react-query'
import { Zap } from 'lucide-react'
import { toast } from 'sonner'
import { billingApi } from '../../api/billing'
import { Button } from '../../components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '../../components/ui/dialog'
import { useUpgradePromptStore } from '../../stores/upgrade-prompt-store'
import { TIER_PRICES } from '../../types/billing'

export function UpgradePromptModal() {
  const { error, close } = useUpgradePromptStore()

  const checkoutMutation = useMutation({
    mutationFn: (tier: string) => billingApi.createCheckout(tier),
    onSuccess: ({ url }) => {
      window.location.href = url
    },
    onError: () => toast.error('Could not start checkout. Please try again.'),
  })

  if (!error) return null

  const suggestedPrice = TIER_PRICES[error.suggestedTier]
  const resourceLabels: Record<string, string> = {
    channels: 'channel connections',
    faqs: 'FAQ entries',
    rules: 'automation rules',
  }
  const resourceLabel = resourceLabels[error.resource] ?? error.resource

  return (
    <Dialog open onOpenChange={(open) => { if (!open) close() }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <div className="mb-3 flex size-10 items-center justify-center rounded-xl bg-primary/10">
            <Zap className="size-5 text-primary" />
          </div>
          <DialogTitle>Upgrade your plan</DialogTitle>
          <DialogDescription>
            You&apos;ve reached the limit of{' '}
            <span className="font-semibold text-slate-900">{error.limit} {resourceLabel}</span>{' '}
            on the <span className="font-semibold text-slate-900">{error.currentTier}</span> plan.
            Upgrade to{' '}
            <span className="font-semibold text-slate-900">{error.suggestedTier}</span> to continue.
          </DialogDescription>
        </DialogHeader>

        <div className="rounded-xl border border-primary/20 bg-primary/5 p-4">
          <div className="text-[13px] font-semibold uppercase tracking-[0.14em] text-slate-500">
            {error.suggestedTier}
          </div>
          <div className="mt-1 text-2xl font-semibold tracking-tight text-slate-950">
            {suggestedPrice === null ? 'Free' : `$${suggestedPrice}`}
            {suggestedPrice !== null && (
              <span className="text-[14px] font-normal text-slate-400">/mo</span>
            )}
          </div>
        </div>

        <DialogFooter className="gap-2">
          <Button type="button" variant="outline" onClick={close}>
            Maybe later
          </Button>
          <Button
            type="button"
            onClick={() => checkoutMutation.mutate(error.suggestedTier)}
            disabled={checkoutMutation.isPending}
          >
            <Zap className="size-4" />
            {checkoutMutation.isPending ? 'Opening checkout…' : `Upgrade to ${error.suggestedTier}`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
