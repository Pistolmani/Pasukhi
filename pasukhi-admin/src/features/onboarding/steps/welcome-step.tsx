import { ChevronRight, Sparkles } from 'lucide-react'
import { PasukhiMark } from '../../../components/brand/pasukhi-mark'
import { Button } from '../../../components/ui/button'

const STEPS_PREVIEW = [
  'Create your business profile',
  'Connect a social channel',
  'Train your bot with FAQs',
  'Configure your AI assistant',
]

export function WelcomeStep({ name, onNext }: { name?: string; onNext: () => void }) {
  return (
    <div className="text-center">
      <div className="relative mx-auto mb-8 flex size-20 items-center justify-center rounded-3xl bg-primary text-white shadow-xl shadow-indigo-500/25">
        <PasukhiMark size={32} />
        <div className="absolute -bottom-2 -right-2 flex size-8 items-center justify-center rounded-xl bg-amber-400 text-slate-900 ring-4 ring-stone-50">
          <Sparkles className="size-4" />
        </div>
      </div>
      <h1 className="text-[32px] font-semibold tracking-tight text-slate-950">
        Welcome{name ? `, ${name}` : ''}!
      </h1>
      <p className="mt-3 text-[16px] leading-relaxed text-slate-500">
        Let&apos;s get your AI-powered inbox up and running. It only takes a few minutes.
      </p>

      <div className="mt-10 space-y-3">
        {STEPS_PREVIEW.map((item, i) => (
          <div
            key={i}
            className="flex items-center gap-3 rounded-xl bg-white px-4 py-3 shadow-sm ring-1 ring-slate-200/60"
          >
            <div className="flex size-6 shrink-0 items-center justify-center rounded-full bg-primary/10 text-[12px] font-semibold text-primary">
              {i + 1}
            </div>
            <span className="text-[14px] font-medium text-slate-700">{item}</span>
          </div>
        ))}
      </div>

      <Button
        size="lg"
        onClick={onNext}
        className="mt-8 h-12 w-full gap-2 text-[15px] font-medium shadow-[0_6px_18px_-8px_rgba(79,70,229,.55)]"
      >
        Let&apos;s get started
        <ChevronRight className="size-4" />
      </Button>
    </div>
  )
}
