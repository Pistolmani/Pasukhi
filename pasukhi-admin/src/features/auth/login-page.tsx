import { zodResolver } from '@hookform/resolvers/zod'
import { Sparkles, Loader2, CheckCircle2, MessageSquare, Bot } from 'lucide-react'
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
    <div className="flex min-h-screen w-full bg-gradient-to-tr from-slate-50 via-[#faf9f6] to-stone-100/60">
      {/* Left side - Login Form */}
      <div className="flex flex-1 flex-col justify-center px-6 py-12 sm:px-12 lg:flex-none lg:w-[480px] xl:w-[560px]">
        <div className="mx-auto w-full max-w-[380px]">
          {/* Mobile Header (Hidden on Desktop) */}
          <div className="mb-10 flex items-center gap-2 text-slate-900 lg:hidden">
            <div className="flex size-8 items-center justify-center rounded-xl bg-gradient-to-br from-amber-400 via-amber-500 to-amber-600 text-slate-950 font-bold">
              <PasukhiMark size={14} />
            </div>
            <span className="text-[15px] font-semibold tracking-tight">Pasukhi</span>
          </div>

          <div className="mb-8">
            <div className="relative mb-6 flex size-14 items-center justify-center rounded-2xl bg-slate-900 text-amber-400 shadow-xl shadow-amber-500/10 ring-1 ring-amber-500/20">
              <PasukhiMark size={20} />
              <div className="absolute -bottom-1 -right-1 flex size-5 items-center justify-center rounded-md bg-amber-400 text-slate-950 ring-4 ring-[#faf9f6] shadow-sm">
                <Sparkles className="size-3 animate-pulse" />
              </div>
            </div>

            <h1 className="text-[26px] font-semibold tracking-tight text-slate-950">Welcome back</h1>
            <p className="mt-2 text-[14px] text-slate-500">Sign in to your account to manage your inbox.</p>
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
                className="h-11 border-slate-200 bg-white text-[14px] shadow-sm transition-all duration-200 hover:border-slate-300 focus:border-amber-500 focus:bg-white focus:ring-4 focus:ring-amber-500/10"
                {...register('email')}
              />
              {errors.email && <p className="text-xs text-rose-600">{errors.email.message}</p>}
            </div>

            <div className="space-y-1.5">
              <div className="flex items-baseline justify-between">
                <Label htmlFor="password" className="text-[12.5px] font-medium text-slate-700">
                  Password
                </Label>
                <span className="cursor-pointer text-[12px] font-medium text-amber-700 transition-colors hover:text-amber-800">
                  Forgot password?
                </span>
              </div>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                className="h-11 border-slate-200 bg-white text-[14px] shadow-sm transition-all duration-200 hover:border-slate-300 focus:border-amber-500 focus:bg-white focus:ring-4 focus:ring-amber-500/10"
                {...register('password')}
              />
              {errors.password && <p className="text-xs text-rose-600">{errors.password.message}</p>}
            </div>

            <label className="group flex w-fit cursor-pointer items-center gap-2 pt-1 text-[13px] text-slate-600">
              <input
                type="checkbox"
                defaultChecked
                className="size-4 rounded border-slate-300 text-amber-600 transition-colors focus:ring-amber-500 focus:ring-offset-0 group-hover:border-amber-500"
              />
              Keep me signed in
            </label>

            <Button
              type="submit"
              size="lg"
              className="luxury-gradient-btn shine-effect mt-4 flex h-11 w-full items-center justify-center gap-2 rounded-xl text-[14px] font-medium text-amber-200 border border-amber-500/20 shadow-[0_4px_20px_-4px_rgba(217,119,6,0.15)] transition-all duration-300 hover:scale-[1.01] hover:text-white hover:border-amber-400/40 hover:shadow-[0_8px_30px_-6px_rgba(217,119,6,0.3)] active:translate-y-[1px] active:shadow-none"
              disabled={isSubmitting}
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="size-4 animate-spin" />
                  Signing in...
                </>
              ) : (
                'Sign in'
              )}
            </Button>

            <div className="flex items-center gap-4 py-3">
              <div className="h-px flex-1 bg-slate-200/60" />
              <span className="text-[11px] font-medium uppercase tracking-widest text-slate-400">or</span>
              <div className="h-px flex-1 bg-slate-200/60" />
            </div>

            <Button
              type="button"
              variant="outline"
              className="group h-11 w-full gap-2.5 rounded-xl border border-slate-200/80 bg-white text-[13.5px] font-medium text-slate-700 shadow-sm transition-all duration-300 hover:bg-slate-50/50 hover:border-slate-300 hover:shadow-md active:scale-[0.99]"
            >
              <span className="relative flex size-4 items-center justify-center overflow-hidden rounded-sm bg-gradient-to-br from-purple-600 via-pink-500 to-amber-400">
                <span className="absolute inset-0 bg-white/20 opacity-0 transition-opacity group-hover:opacity-100" />
              </span>
              Continue with Meta Business
            </Button>
          </form>

          <div className="mt-8 text-[12.5px] text-slate-500">
            Don&apos;t have an account?{' '}
            <span className="cursor-pointer font-medium text-amber-700 hover:text-amber-800">Contact sales</span>
          </div>
        </div>
      </div>

      {/* Right side - Abstract Visual Panel */}
      <div className="relative hidden flex-1 overflow-hidden bg-[#070608] lg:block">
        {/* Swirling celestial backdrops */}
        <div className="aura-glow-1 absolute -left-1/4 -top-1/4 size-[800px] rounded-full bg-gradient-to-br from-purple-800/15 to-amber-600/5 blur-[130px]" />
        <div className="aura-glow-2 absolute -bottom-1/4 -right-1/4 size-[800px] rounded-full bg-gradient-to-tr from-amber-600/10 to-pink-600/5 blur-[130px]" />
        
        {/* Premium texture overlay */}
        <div className="absolute inset-0 bg-[url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI0IiBoZWlnaHQ9IjQiPgoJPHJlY3Qgd2lkdGg9IjQiIGhlaWdodD0iNCIgZmlsbD0iI2ZmZiIgZmlsbC1vcGFjaXR5PSIwLjA1Ii8+Cjwvc3ZnPg==')] opacity-15 mix-blend-overlay" />

        <div className="relative z-10 flex h-full flex-col justify-between p-12 xl:p-16">
          <div className="flex items-center gap-2.5 text-white">
            <div className="flex size-9 items-center justify-center rounded-xl bg-gradient-to-br from-amber-400 via-amber-500 to-amber-600 text-slate-950 font-bold shadow-lg shadow-amber-500/10">
              <PasukhiMark size={18} />
            </div>
            <span className="text-[18px] font-semibold tracking-tight">Pasukhi</span>
          </div>

          <div className="my-auto max-w-[520px]">
            {/* Swirling floating glass blocks */}
            <div className="mb-14 flex flex-col gap-6">
              <div className="animate-float-slow flex w-fit items-center gap-4 rounded-2xl border border-white/10 bg-white/[0.03] p-4 pr-16 shadow-[0_20px_50px_-12px_rgba(0,0,0,0.5)] backdrop-blur-lg">
                <div className="flex size-11 items-center justify-center rounded-full bg-amber-500/10 text-amber-300">
                  <MessageSquare className="size-5" />
                </div>
                <div className="space-y-2.5">
                  <div className="h-2 w-28 rounded-full bg-white/25" />
                  <div className="h-2 w-40 rounded-full bg-white/10" />
                </div>
              </div>

              <div className="animate-float-medium flex w-fit translate-x-12 items-center gap-4 rounded-2xl border border-amber-500/20 bg-amber-500/[0.04] p-4 pl-16 shadow-[0_20px_50px_-12px_rgba(217,119,6,0.15)] backdrop-blur-lg">
                <div className="space-y-2.5 text-right">
                  <div className="flex items-center justify-end gap-1.5">
                    <span className="text-[11px] font-semibold uppercase tracking-wider text-amber-400">Resolved by AI</span>
                    <CheckCircle2 className="size-3.5 text-amber-400" />
                  </div>
                  <div className="h-2 w-48 rounded-full bg-amber-400/20" />
                </div>
                <div className="flex size-11 items-center justify-center rounded-full bg-amber-500/10 text-amber-400">
                  <Bot className="size-5" />
                </div>
              </div>
            </div>

            <h2 className="text-4xl font-semibold leading-[1.1] tracking-tight text-white xl:text-[44px]">
              Automate your <br />
              <span className="bg-gradient-to-r from-white via-amber-100 to-orange-300 bg-clip-text text-transparent">
                customer success.
              </span>
            </h2>
            <p className="mt-5 text-[17px] leading-relaxed text-slate-400">
              Turn conversations into conversions. Connect your channels, train your AI, and watch your business scale on autopilot.
            </p>
          </div>
          
          <div className="flex items-center justify-between text-[13px] text-slate-500">
            <span>© {new Date().getFullYear()} Pasukhi Inc.</span>
            <div className="flex gap-4">
              <span className="cursor-pointer hover:text-slate-300">Privacy Policy</span>
              <span className="cursor-pointer hover:text-slate-300">Terms of Service</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
