import { zodResolver } from '@hookform/resolvers/zod'
import { Bot, CheckCircle2, Eye, EyeOff, Loader2, MessageSquare, Sparkles } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { authApi } from '../../api/auth'
import { PasukhiMark } from '../../components/brand/pasukhi-mark'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import { loginSchema, type LoginFormData } from '../../schemas/auth-schemas'
import { useAuthStore } from '../../stores/auth-store'

export function LoginPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const setAuth = useAuthStore((state) => state.setAuth)
  const [showPassword, setShowPassword] = useState(false)
  const [googleLoading, setGoogleLoading] = useState(false)

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
  })

  useEffect(() => {
    const code = searchParams.get('code')
    if (!code) return

    setGoogleLoading(true)
    authApi
      .googleCallback(code, window.location.origin + '/login')
      .then((result) => {
        setAuth(result.user, result.accessToken)
        navigate('/', { replace: true })
      })
      .catch(() => {
        setError('root', { message: 'Google sign-in failed. Please try again.' })
        setGoogleLoading(false)
        window.history.replaceState({}, '', '/login')
      })
  }, [searchParams, setAuth, navigate, setError])

  const onSubmit = async (data: LoginFormData) => {
    try {
      const result = await authApi.login(data)
      setAuth(result.user, result.accessToken)
      navigate('/')
    } catch {
      setError('root', { message: 'Invalid email or password' })
    }
  }

  const handleGoogleClick = () => {
    const params = new URLSearchParams({
      client_id: import.meta.env.VITE_GOOGLE_CLIENT_ID,
      redirect_uri: window.location.origin + '/login',
      response_type: 'code',
      scope: 'openid email profile',
      access_type: 'offline',
    })
    window.location.href = `https://accounts.google.com/o/oauth2/v2/auth?${params}`
  }

  return (
    <div className="flex min-h-screen w-full bg-gradient-to-tr from-slate-50 via-[#faf9f6] to-stone-100/60">
      {/* Left side - Login Form */}
      <div className="flex flex-1 flex-col justify-center px-6 py-12 sm:px-12 lg:flex-none lg:w-[480px] lg:border-r lg:border-slate-200/70 xl:w-[560px]">
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
                <Link
                  to="/forgot-password"
                  className="text-[12px] font-medium text-amber-700 transition-colors hover:text-amber-800 hover:underline"
                >
                  Forgot password?
                </Link>
              </div>
              <div className="relative">
                <Input
                  id="password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  className="h-11 border-slate-200 bg-white pr-10 text-[14px] shadow-sm transition-all duration-200 hover:border-slate-300 focus:border-amber-500 focus:bg-white focus:ring-4 focus:ring-amber-500/10"
                  {...register('password')}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((s) => !s)}
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 transition-colors hover:text-slate-600"
                >
                  {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
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

            {errors.root && (
              <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-center text-[12.5px] font-medium text-rose-700">
                {errors.root.message}
              </div>
            )}

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

            <div className="flex flex-col gap-3">
              <Button
                type="button"
                onClick={handleGoogleClick}
                variant="outline"
                disabled={googleLoading}
                className="group h-11 w-full gap-2.5 rounded-xl border border-slate-200/80 bg-white text-[13.5px] font-medium text-slate-700 shadow-sm transition-all duration-300 hover:bg-slate-50/50 hover:border-slate-300 hover:shadow-md active:scale-[0.99]"
              >
                {googleLoading ? (
                  <>
                    <Loader2 className="size-4 animate-spin" />
                    Connecting...
                  </>
                ) : (
                  <>
                    <svg className="size-4" viewBox="0 0 24 24">
                      <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4"/>
                      <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
                      <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l3.66-2.84z" fill="#FBBC05"/>
                      <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
                    </svg>
                    Continue with Google
                  </>
                )}
              </Button>

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
            </div>
          </form>

          <div className="mt-8 text-[12.5px] text-slate-500">
            Don&apos;t have an account?{' '}
            <a
              href="mailto:hello@pasukhi.com"
              className="font-medium text-amber-700 transition-colors hover:text-amber-800"
            >
              Contact sales
            </a>
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
              <Link to="/privacy" className="transition-colors hover:text-slate-300 hover:underline">
                Privacy Policy
              </Link>
              <Link to="/terms" className="transition-colors hover:text-slate-300 hover:underline">
                Terms of Service
              </Link>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
