import { zodResolver } from '@hookform/resolvers/zod'
import { Sparkles } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { authApi } from '../../api/auth'
import { PasukhiMark } from '../../components/brand/pasukhi-mark'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import { loginSchema, type LoginFormData } from '../../schemas/auth-schemas'
import { useAuthStore } from '../../stores/auth-store'

export function LoginPage() {
  const navigate = useNavigate()
  const setAuth = useAuthStore((state) => state.setAuth)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: 'nika@khinkalihouse.ge',
      password: '',
    },
  })

  const onSubmit = async (data: LoginFormData) => {
    try {
      const result = await authApi.login(data)
      setAuth(result.user, result.accessToken)
      navigate('/')
    } catch {
      toast.error('Invalid email or password')
    }
  }

  return (
    <div className="auth-bg relative flex min-h-screen items-center justify-center overflow-hidden p-6">
      <div className="absolute left-6 top-6 flex items-center gap-2 text-slate-700">
        <div className="flex size-9 items-center justify-center rounded-xl bg-primary text-white">
          <PasukhiMark size={16} />
        </div>
        <div className="leading-tight">
          <div className="text-[14px] font-semibold tracking-tight">Pasukhi</div>
          <div className="text-[10.5px] uppercase tracking-[0.18em] text-slate-500">Answer faster. Sell more.</div>
        </div>
      </div>

      <div className="absolute right-6 top-6 hidden text-[12.5px] text-slate-500 sm:block">
        Need an account? <span className="ml-1 font-medium text-indigo-600">Contact sales</span>
      </div>

      <div className="w-full max-w-[420px]">
        <form
          onSubmit={handleSubmit(onSubmit)}
          className="card-shadow rounded-3xl border border-border bg-white p-8"
        >
          <div className="relative mx-auto mb-6 flex size-16 items-center justify-center rounded-2xl bg-primary text-white">
            <PasukhiMark size={20} />
            <div className="absolute -bottom-1.5 -right-1.5 flex size-6 items-center justify-center rounded-lg bg-amber-400 text-slate-900 ring-4 ring-white">
              <Sparkles className="size-3" />
            </div>
          </div>

          <div className="space-y-1 text-center">
            <h1 className="text-[22px] font-semibold tracking-tight text-slate-950">Welcome back</h1>
            <p className="text-[13.5px] text-slate-500">Sign in to manage your inbox and bot.</p>
          </div>

          <div className="mt-6 space-y-3">
            <div className="space-y-1.5">
              <Label htmlFor="email" className="text-[12px] font-medium text-slate-600">
                Email
              </Label>
              <Input
                id="email"
                type="email"
                autoComplete="email"
                className="h-10 border-border bg-white text-[13.5px] focus-visible:ring-indigo-500/20"
                {...register('email')}
              />
              {errors.email && <p className="text-xs text-rose-600">{errors.email.message}</p>}
            </div>

            <div className="space-y-1.5">
              <div className="flex items-baseline justify-between">
                <Label htmlFor="password" className="text-[12px] font-medium text-slate-600">
                  Password
                </Label>
                <span className="text-[11.5px] font-medium text-indigo-600">Forgot?</span>
              </div>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                className="h-10 border-border bg-white text-[13.5px] focus-visible:ring-indigo-500/20"
                {...register('password')}
              />
              {errors.password && <p className="text-xs text-rose-600">{errors.password.message}</p>}
            </div>

            <label className="flex items-center gap-2 pt-1 text-[12.5px] text-slate-600">
              <input
                type="checkbox"
                defaultChecked
                className="size-3.5 rounded border-border text-indigo-600 focus:ring-indigo-500"
              />
              Stay signed in for 30 days
            </label>

            <Button
              type="submit"
              size="lg"
              className="mt-2 h-11 w-full shadow-[0_6px_18px_-8px_rgba(79,70,229,.55),inset_0_1px_0_rgba(255,255,255,.18)]"
              disabled={isSubmitting}
            >
              {isSubmitting ? 'Signing in...' : 'Sign in'}
            </Button>

            <div className="flex items-center gap-3 py-1">
              <div className="h-px flex-1 bg-border" />
              <span className="text-[11px] uppercase tracking-[0.16em] text-slate-400">or</span>
              <div className="h-px flex-1 bg-border" />
            </div>

            <Button type="button" variant="outline" className="h-10 w-full gap-2 bg-white text-[13px] text-slate-700">
              <span className="size-4 rounded-sm bg-gradient-to-br from-pink-600 via-orange-500 to-amber-300" />
              Continue with Meta Business
            </Button>
          </div>
        </form>

        <div className="mt-4 text-center text-[11.5px] text-slate-500">
          By signing in you agree to our <span className="underline decoration-dotted">Terms</span> and{' '}
          <span className="underline decoration-dotted">Privacy</span>.
        </div>
      </div>
    </div>
  )
}
