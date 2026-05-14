import { zodResolver } from '@hookform/resolvers/zod'
import {
  Bot,
  BrainCircuit,
  CheckCircle2,
  ChevronRight,
  GitBranchPlus,
  HelpCircle,
  Loader2,
  MessageSquare,
  Rocket,
  Sparkles,
} from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { authApi } from '../../api/auth'
import { PasukhiMark } from '../../components/brand/pasukhi-mark'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import { useAuthStore } from '../../stores/auth-store'

const TOTAL_STEPS = 6

const businessSchema = z.object({
  name: z.string().min(2, 'Business name must be at least 2 characters'),
  description: z.string().optional(),
})
type BusinessFormData = z.infer<typeof businessSchema>

const INFO_STEPS = [
  {
    icon: MessageSquare,
    iconColor: 'text-indigo-400',
    iconBg: 'bg-indigo-500/15',
    title: 'Connect your channels',
    description:
      'Link your Instagram, Messenger, or WhatsApp account so Pasukhi can receive and reply to customer messages automatically.',
    hint: "You'll find this under Channels in the sidebar.",
  },
  {
    icon: HelpCircle,
    iconColor: 'text-amber-400',
    iconBg: 'bg-amber-500/15',
    title: 'Train your bot with FAQs',
    description:
      'Add the most common questions your customers ask and the best answers for each. The bot matches these first before using AI.',
    hint: "Head to FAQs in the sidebar to get started.",
  },
  {
    icon: GitBranchPlus,
    iconColor: 'text-emerald-400',
    iconBg: 'bg-emerald-500/15',
    title: 'Set up automation rules',
    description:
      'Create keyword-triggered rules to handle specific scenarios — promotions, order status, opening hours — with zero AI cost.',
    hint: "Find this under Rules in the sidebar.",
  },
  {
    icon: BrainCircuit,
    iconColor: 'text-violet-400',
    iconBg: 'bg-violet-500/15',
    title: 'Configure your AI assistant',
    description:
      "Tell the AI who you are, what you sell, and how you'd like it to speak. It handles everything your FAQs and rules don't cover.",
    hint: "Configure this under AI Settings in the sidebar.",
  },
]

export function OnboardingPage() {
  const [step, setStep] = useState(1)
  const setAuth = useAuthStore((state) => state.setAuth)
  const user = useAuthStore((state) => state.user)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<BusinessFormData>({
    resolver: zodResolver(businessSchema),
  })

  const onCreateBusiness = async (data: BusinessFormData) => {
    const result = await authApi.setupBusiness(data.name, data.description)
    setAuth(result.user, result.accessToken)
    setStep(3)
  }

  const progressPercent = ((step - 1) / (TOTAL_STEPS - 1)) * 100

  return (
    <div className="flex min-h-screen flex-col bg-stone-50">
      {/* Header */}
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

      {/* Progress bar */}
      <div className="h-1 w-full bg-slate-100">
        <div
          className="h-full bg-primary transition-all duration-500 ease-out"
          style={{ width: `${progressPercent}%` }}
        />
      </div>

      {/* Content */}
      <div className="flex flex-1 items-center justify-center px-6 py-12">
        <div className="w-full max-w-[520px]">
          {step === 1 && <WelcomeStep name={user?.firstName} onNext={() => setStep(2)} />}
          {step === 2 && (
            <CreateBusinessStep
              onSubmit={handleSubmit(onCreateBusiness)}
              register={register}
              errors={errors}
              isSubmitting={isSubmitting}
            />
          )}
          {step >= 3 && step <= 6 && (
            <InfoStep
              stepData={INFO_STEPS[step - 3]}
              stepNumber={step}
              totalSteps={TOTAL_STEPS}
              isLast={step === 6}
              onNext={() => {
                if (step === 6) {
                  window.location.href = '/'
                } else {
                  setStep(step + 1)
                }
              }}
            />
          )}
        </div>
      </div>
    </div>
  )
}

function WelcomeStep({ name, onNext }: { name?: string; onNext: () => void }) {
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
        Let's get your AI-powered inbox up and running. It only takes a few minutes.
      </p>

      <div className="mt-10 space-y-3">
        {[
          'Create your business profile',
          'Connect your social channels',
          'Train your bot with FAQs & rules',
          'Configure your AI assistant',
        ].map((item, i) => (
          <div key={i} className="flex items-center gap-3 rounded-xl bg-white px-4 py-3 shadow-sm ring-1 ring-slate-200/60">
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
        Let's get started
        <ChevronRight className="size-4" />
      </Button>
    </div>
  )
}

function CreateBusinessStep({
  onSubmit,
  register,
  errors,
  isSubmitting,
}: {
  onSubmit: (e: React.FormEvent) => void
  register: ReturnType<typeof useForm<BusinessFormData>>['register']
  errors: ReturnType<typeof useForm<BusinessFormData>>['formState']['errors']
  isSubmitting: boolean
}) {
  return (
    <div>
      <div className="mb-8">
        <div className="mb-4 flex size-12 items-center justify-center rounded-2xl bg-indigo-50 text-primary">
          <Bot className="size-6" />
        </div>
        <h2 className="text-[26px] font-semibold tracking-tight text-slate-950">Create your business</h2>
        <p className="mt-2 text-[14px] text-slate-500">
          This is the name your team and bot will use. You can change it later.
        </p>
      </div>

      <form onSubmit={onSubmit} className="space-y-5">
        <div className="space-y-1.5">
          <Label htmlFor="name" className="text-[12.5px] font-medium text-slate-700">
            Business name <span className="text-rose-500">*</span>
          </Label>
          <Input
            id="name"
            autoFocus
            placeholder="e.g. Bloom Flowers"
            className="h-11 border-slate-200 bg-white text-[14px] focus:border-indigo-500 focus:ring-4 focus:ring-indigo-500/10"
            {...register('name')}
          />
          {errors.name && <p className="text-xs text-rose-600">{errors.name.message}</p>}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="description" className="text-[12.5px] font-medium text-slate-700">
            Description <span className="text-slate-400 font-normal">(optional)</span>
          </Label>
          <Input
            id="description"
            placeholder="What does your business sell or offer?"
            className="h-11 border-slate-200 bg-white text-[14px] focus:border-indigo-500 focus:ring-4 focus:ring-indigo-500/10"
            {...register('description')}
          />
        </div>

        <Button
          type="submit"
          size="lg"
          disabled={isSubmitting}
          className="mt-2 h-12 w-full gap-2 text-[15px] font-medium shadow-[0_6px_18px_-8px_rgba(79,70,229,.55)]"
        >
          {isSubmitting ? (
            <>
              <Loader2 className="size-4 animate-spin" />
              Creating...
            </>
          ) : (
            <>
              Create business
              <ChevronRight className="size-4" />
            </>
          )}
        </Button>
      </form>
    </div>
  )
}

function InfoStep({
  stepData,
  stepNumber,
  totalSteps,
  isLast,
  onNext,
}: {
  stepData: (typeof INFO_STEPS)[0]
  stepNumber: number
  totalSteps: number
  isLast: boolean
  onNext: () => void
}) {
  const Icon = stepData.icon

  return (
    <div>
      <div className="mb-8">
        <div className={`mb-4 flex size-14 items-center justify-center rounded-2xl ${stepData.iconBg}`}>
          <Icon className={`size-7 ${stepData.iconColor}`} />
        </div>
        <p className="mb-1 text-[12px] font-semibold uppercase tracking-widest text-slate-400">
          Step {stepNumber} of {totalSteps}
        </p>
        <h2 className="text-[26px] font-semibold tracking-tight text-slate-950">{stepData.title}</h2>
        <p className="mt-3 text-[15px] leading-relaxed text-slate-500">{stepData.description}</p>
      </div>

      <div className="mb-8 flex items-start gap-2.5 rounded-xl bg-slate-50 px-4 py-3 ring-1 ring-slate-200/60">
        <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-emerald-500" />
        <p className="text-[13px] text-slate-600">{stepData.hint}</p>
      </div>

      <Button
        size="lg"
        onClick={onNext}
        className="h-12 w-full gap-2 text-[15px] font-medium shadow-[0_6px_18px_-8px_rgba(79,70,229,.55)]"
      >
        {isLast ? (
          <>
            <Rocket className="size-4" />
            Launch dashboard
          </>
        ) : (
          <>
            Next
            <ChevronRight className="size-4" />
          </>
        )}
      </Button>
      {!isLast && (
        <button onClick={onNext} className="mt-3 w-full text-center text-[13px] text-slate-400 hover:text-slate-600">
          Skip this step
        </button>
      )}
    </div>
  )
}
