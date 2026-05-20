import { CheckCircle2, Rocket } from 'lucide-react'
import { Button } from '../../../components/ui/button'

const DONE_ITEMS = [
  'Business profile created',
  'Channel connected',
  'FAQs reviewed',
  'AI personality configured',
]

export function LaunchStep({ onFinish }: { onFinish: () => void }) {
  return (
    <div className="text-center">
      <div className="mx-auto mb-8 flex size-20 items-center justify-center rounded-3xl bg-emerald-500/15 text-emerald-500">
        <Rocket className="size-10" />
      </div>
      <h1 className="text-[32px] font-semibold tracking-tight text-slate-950">
        Your bot is ready!
      </h1>
      <p className="mt-3 text-[16px] leading-relaxed text-slate-500">
        Pasukhi will start replying to customer messages automatically. You can fine-tune everything
        from the dashboard at any time.
      </p>

      <div className="mt-10 space-y-3">
        {DONE_ITEMS.map((item) => (
          <div
            key={item}
            className="flex items-center gap-3 rounded-xl bg-white px-4 py-3 shadow-sm ring-1 ring-slate-200/60"
          >
            <CheckCircle2 className="size-5 shrink-0 text-emerald-500" />
            <span className="text-[14px] font-medium text-slate-700">{item}</span>
          </div>
        ))}
      </div>

      <Button
        size="lg"
        onClick={onFinish}
        className="mt-8 h-12 w-full gap-2 text-[15px] font-medium shadow-[0_6px_18px_-8px_rgba(79,70,229,.55)]"
      >
        <Rocket className="size-4" />
        Launch dashboard
      </Button>
    </div>
  )
}
