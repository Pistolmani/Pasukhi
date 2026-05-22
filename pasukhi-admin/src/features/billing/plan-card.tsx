import { Check } from 'lucide-react'
import { Button } from '../../components/ui/button'
import type { SubscriptionTier } from '../../types/billing'

interface PlanCardProps {
  tier: SubscriptionTier
  price: number | null
  features: string[]
  isCurrentPlan: boolean
  onSelect: () => void
  isLoading?: boolean
  disabled?: boolean
}

export function PlanCard({
  tier,
  price,
  features,
  isCurrentPlan,
  onSelect,
  isLoading,
  disabled,
}: PlanCardProps) {
  return (
    <div
      className={[
        'flex flex-col rounded-2xl border p-6 transition-all',
        isCurrentPlan
          ? 'border-primary bg-primary/5 ring-1 ring-primary/20'
          : 'border-border bg-white',
      ].join(' ')}
    >
      <div className="flex items-start justify-between">
        <div>
          <div className="text-[13px] font-semibold uppercase tracking-[0.14em] text-slate-500">{tier}</div>
          <div className="mt-1 text-3xl font-semibold tracking-tight text-slate-950">
            {price === null ? 'Free' : `$${price}`}
            {price !== null && <span className="text-[15px] font-normal text-slate-400">/mo</span>}
          </div>
        </div>
        {isCurrentPlan && (
          <span className="rounded-full bg-primary/10 px-2.5 py-1 text-[11.5px] font-semibold text-primary">
            Current
          </span>
        )}
      </div>

      <ul className="mt-5 flex-1 space-y-2.5">
        {features.map((feature) => (
          <li key={feature} className="flex items-start gap-2 text-[13px] text-slate-600">
            <Check className="mt-0.5 size-4 shrink-0 text-emerald-500" />
            {feature}
          </li>
        ))}
      </ul>

      <Button
        type="button"
        variant={isCurrentPlan ? 'outline' : 'default'}
        className="mt-6 w-full"
        onClick={onSelect}
        disabled={disabled || isCurrentPlan || isLoading}
      >
        {isCurrentPlan ? 'Current plan' : `Upgrade to ${tier}`}
      </Button>
    </div>
  )
}
