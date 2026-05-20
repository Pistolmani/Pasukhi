import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowLeft, Loader2, Sparkles } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { z } from 'zod'
import { PasukhiMark } from '../../components/brand/pasukhi-mark'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'

const schema = z.object({
  email: z.string().email('Enter a valid email'),
})

type FormData = z.infer<typeof schema>

export function ForgotPasswordPage() {
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    reset,
  } = useForm<FormData>({ resolver: zodResolver(schema) })

  const onSubmit = async (_data: FormData) => {
    await new Promise((resolve) => setTimeout(resolve, 600))
    toast.success('If that email exists, a reset link has been sent.')
    reset()
  }

  return (
    <div className="flex min-h-screen w-full items-center justify-center bg-stone-50 px-6 py-12">
      <div className="w-full max-w-[420px]">
        <div className="mb-8">
          <div className="relative mb-6 flex size-14 items-center justify-center rounded-2xl bg-primary text-white shadow-lg shadow-indigo-500/20">
            <PasukhiMark size={20} />
            <div className="absolute -bottom-1 -right-1 flex size-5 items-center justify-center rounded-md bg-amber-400 text-slate-900 ring-4 ring-stone-50">
              <Sparkles className="size-3 animate-pulse" />
            </div>
          </div>
          <h1 className="text-[26px] font-semibold tracking-tight text-slate-950">Reset your password</h1>
          <p className="mt-2 text-[14px] text-slate-500">
            Enter your email and we&apos;ll send you a link to reset your password.
          </p>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
          <div className="space-y-1.5">
            <Label htmlFor="email" className="text-[12.5px] font-medium text-slate-700">
              Email address
            </Label>
            <Input
              id="email"
              type="email"
              autoComplete="email"
              className="h-11 border-slate-200 bg-white text-[14px] transition-all duration-200 focus:border-indigo-500 focus:ring-4 focus:ring-indigo-500/10"
              {...register('email')}
            />
            {errors.email && <p className="text-xs text-rose-600">{errors.email.message}</p>}
          </div>

          <Button
            type="submit"
            size="lg"
            className="flex h-11 w-full items-center justify-center gap-2 text-[14px] font-medium shadow-[0_6px_18px_-8px_rgba(79,70,229,.55),inset_0_1px_0_rgba(255,255,255,.18)] transition-all hover:translate-y-[-1px] hover:shadow-[0_8px_20px_-8px_rgba(79,70,229,.65)] active:translate-y-[1px]"
            disabled={isSubmitting}
          >
            {isSubmitting ? (
              <>
                <Loader2 className="size-4 animate-spin" />
                Sending…
              </>
            ) : (
              'Send reset link'
            )}
          </Button>
        </form>

        <Link
          to="/login"
          className="mt-6 inline-flex items-center gap-1.5 text-[13px] font-medium text-indigo-600 transition-colors hover:text-indigo-700 hover:underline"
        >
          <ArrowLeft className="size-3.5" />
          Back to sign in
        </Link>
      </div>
    </div>
  )
}
