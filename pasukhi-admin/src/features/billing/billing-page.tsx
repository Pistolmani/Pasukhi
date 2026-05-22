import { useMutation } from '@tanstack/react-query'
import { CreditCard, ExternalLink } from 'lucide-react'
import { toast } from 'sonner'
import { billingApi } from '../../api/billing'
import { Button } from '../../components/ui/button'
import type { SubscriptionTier } from '../../types/billing'
import { TIER_ORDER, TIER_PRICES } from '../../types/billing'
import { PlanCard } from './plan-card'
import { useBillingStatus } from './use-billing-status'

const TIER_FEATURES: Record<SubscriptionTier, string[]> = {
  Free: ['1 channel connection', '5 FAQs', '3 automation rules', 'Basic analytics'],
  Starter: ['2 channel connections', '25 FAQs', '10 automation rules', 'AI replies (50k tokens/day)', 'Messenger ice breakers', 'Basic analytics'],
  Pro: ['3 channel connections', 'Unlimited FAQs', 'Unlimited rules', 'AI replies (500k tokens/day)', 'Full analytics (90-day history)', 'Messenger ice breakers'],
  Agency: ['Unlimited channels', 'Unlimited FAQs & rules', 'AI replies (2M tokens/day)', 'Full analytics', 'Priority support'],
  Enterprise: ['Everything in Agency', 'Unlimited AI tokens', 'Priority support', 'Custom SLA'],
}

export function BillingPage() {
  const statusQuery = useBillingStatus()
  const currentTier = statusQuery.data?.tier ?? 'Free'
  const hasSubscription = statusQuery.data?.hasStripeSubscription

  const checkoutMutation = useMutation({
    mutationFn: (tier: string) => billingApi.createCheckout(tier),
    onSuccess: ({ url }) => { window.location.href = url },
    onError: () => toast.error('Could not start checkout. Please try again.'),
  })

  const portalMutation = useMutation({
    mutationFn: billingApi.createPortal,
    onSuccess: ({ url }) => { window.location.href = url },
    onError: () => toast.error('Could not open billing portal. Please try again.'),
  })

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <div className="text-[12px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            Subscription
          </div>
          <h2 className="mt-1 text-[26px] font-semibold tracking-tight text-slate-950">Billing</h2>
          <p className="mt-1 text-[13.5px] text-slate-500">
            You are on the{' '}
            <span className="font-semibold text-slate-900">{currentTier}</span> plan.
            {statusQuery.data?.currentPeriodEnd && (
              <> Renews {new Date(statusQuery.data.currentPeriodEnd).toLocaleDateString()}.</>
            )}
          </p>
        </div>
        {hasSubscription && (
          <Button
            type="button"
            variant="outline"
            onClick={() => portalMutation.mutate()}
            disabled={portalMutation.isPending}
          >
            <CreditCard className="size-4" />
            {portalMutation.isPending ? 'Opening…' : 'Manage subscription'}
            <ExternalLink className="size-3.5 text-slate-400" />
          </Button>
        )}
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {TIER_ORDER.map((tier) => (
          <PlanCard
            key={tier}
            tier={tier}
            price={TIER_PRICES[tier]}
            features={TIER_FEATURES[tier]}
            isCurrentPlan={tier === currentTier}
            isLoading={checkoutMutation.isPending}
            onSelect={() => checkoutMutation.mutate(tier)}
          />
        ))}
      </div>

      {statusQuery.data && (
        <section className="card-shadow rounded-2xl border border-border bg-white p-5">
          <h3 className="text-[13px] font-semibold text-slate-950">Current plan limits</h3>
          <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
            <LimitRow label="Channels" value={statusQuery.data.limits.maxChannels} />
            <LimitRow label="FAQs" value={statusQuery.data.limits.maxFaqs} />
            <LimitRow label="Rules" value={statusQuery.data.limits.maxRules} />
            <LimitRow label="AI tokens / day" value={statusQuery.data.limits.maxAiTokensPerDay} />
          </div>
        </section>
      )}
    </div>
  )
}

function LimitRow({ label, value }: { label: string; value: number }) {
  const display = value >= 2_000_000_000 ? 'Unlimited' : value.toLocaleString()
  return (
    <div className="rounded-xl border border-border p-3">
      <div className="text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-500">{label}</div>
      <div className="mt-1 text-[18px] font-semibold tabular-nums text-slate-950">{display}</div>
    </div>
  )
}
