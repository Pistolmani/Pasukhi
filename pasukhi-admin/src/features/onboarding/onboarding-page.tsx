import { PasukhiMark } from '../../components/brand/pasukhi-mark'
import { useAuthStore } from '../../stores/auth-store'
import { AiPersonalityStep } from './steps/ai-personality-step'
import { BusinessQuestionsStep } from './steps/business-questions-step'
import { BusinessStep } from './steps/business-step'
import { ChannelStep } from './steps/channel-step'
import { LaunchStep } from './steps/launch-step'
import { SuggestedFaqsStep } from './steps/suggested-faqs-step'
import { WelcomeStep } from './steps/welcome-step'
import { useState } from 'react'

const TOTAL_STEPS = 7
const SKIPPABLE_FROM = 3

export function OnboardingPage() {
  const [step, setStep] = useState(1)
  const user = useAuthStore((state) => state.user)

  const next = () => setStep((s) => Math.min(s + 1, TOTAL_STEPS))
  const finish = () => { window.location.href = '/' }
  const progressPercent = ((step - 1) / (TOTAL_STEPS - 1)) * 100
  const canSkip = step >= SKIPPABLE_FROM && step < TOTAL_STEPS

  return (
    <div className="flex min-h-screen flex-col bg-stone-50">
      <header className="flex items-center justify-between border-b border-slate-200/70 bg-white/80 px-6 py-4 backdrop-blur-sm">
        <div className="flex items-center gap-2">
          <div className="flex size-8 items-center justify-center rounded-xl bg-primary text-white">
            <PasukhiMark size={14} />
          </div>
          <span className="text-[15px] font-semibold tracking-tight text-slate-900">Pasukhi</span>
        </div>
        <span className="text-[13px] font-medium text-slate-400">
          Step {step} of {TOTAL_STEPS}
        </span>
      </header>

      <div className="h-1 w-full bg-slate-100">
        <div
          className="h-full bg-primary transition-all duration-500 ease-out"
          style={{ width: `${progressPercent}%` }}
        />
      </div>

      <div className="flex flex-1 items-center justify-center px-6 py-12">
        <div className="w-full max-w-[520px]">
          {step === 1 && <WelcomeStep name={user?.firstName} onNext={next} />}
          {step === 2 && <BusinessStep onNext={next} />}
          {step === 3 && <ChannelStep onNext={next} />}
          {step === 4 && <BusinessQuestionsStep onNext={next} />}
          {step === 5 && <SuggestedFaqsStep onNext={next} />}
          {step === 6 && <AiPersonalityStep onNext={next} />}
          {step === 7 && <LaunchStep onFinish={finish} />}

          {canSkip && (
            <button
              onClick={next}
              className="mt-4 w-full text-center text-[13px] text-slate-400 hover:text-slate-600"
            >
              Skip for now
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
