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
  const [metaLoading, setMetaLoading] = useState(false)

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

    setMetaLoading(true)
    authApi
      .metaCallback(code, window.location.origin + '/login')
      .then((result) => {
        setAuth(result.user, result.accessToken)
        navigate('/', { replace: true })
      })
      .catch(() => {
        setError('root', { message: 'Meta sign-in failed. Please try again.' })
        setMetaLoading(false)
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

  const handleMetaClick = () => {
    const appId = import.meta.env.VITE_META_APP_ID
    if (!appId) {
      setError('root', { message: 'Meta Business sign-in is not configured yet.' })
      return
    }
    const params = new URLSearchParams({
      client_id: appId,
      redirect_uri: window.location.origin + '/login',
      scope: 'email,public_profile',
      response_type: 'code',
      state: crypto.randomUUID(),
    })
    window.location.href = `https://www.facebook.com/v21.0/dialog/oauth?${params}`
  }

  return (
    <div className="flex min-h-screen w-full bg-stone-50">
      {/* Left side - Login Form */}
      <div className="flex flex-1 flex-col justify-center bg-stone-50 px-6 py-12 sm:px-12 lg:flex-none lg:w-[480px] lg:border-r lg:border-slate-200/70 xl:w-[560px]">
        <div className="mx-auto w-full max-w-[380px]">
          {/* Mobile Header (Hidden on Desktop) */}
          <div className="mb-10 flex items-center gap-2 text-slate-900 lg:hidden">
            <div className="flex size-8 items-center justify-center rounded-xl bg-primary text-white">
              <PasukhiMark size={14} />
            </div>
            <span className="text-[15px] font-semibold tracking-tight">Pasukhi</span>
          </div>

          <div className="mb-8">
            <div className="relative mb-6 flex size-14 items-center justify-center rounded-2xl bg-primary text-white shadow-lg shadow-indigo-500/20">
              <PasukhiMark size={20} />
              <div className="absolute -bottom-1 -right-1 flex size-5 items-center justify-center rounded-md bg-amber-400 text-slate-900 ring-4 ring-stone-50">
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
                className="h-11 border-slate-200 bg-white text-[14px] transition-all duration-200 focus:border-indigo-500 focus:ring-4 focus:ring-indigo-500/10"
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
                  className="text-[12px] font-medium text-indigo-600 transition-colors hover:text-indigo-700 hover:underline"
                >
                  Forgot password?
                </Link>
              </div>
              <div className="relative">
                <Input
                  id="password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  className="h-11 border-slate-200 bg-white pr-10 text-[14px] transition-all duration-200 focus:border-indigo-500 focus:ring-4 focus:ring-indigo-500/10"
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
                className="size-4 rounded border-slate-300 text-indigo-600 transition-colors focus:ring-indigo-500 focus:ring-offset-0 group-hover:border-indigo-500"
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
              className="mt-4 flex h-11 w-full items-center justify-center gap-2 text-[14px] font-medium shadow-[0_6px_18px_-8px_rgba(79,70,229,.55),inset_0_1px_0_rgba(255,255,255,.18)] transition-all hover:translate-y-[-1px] hover:shadow-[0_8px_20px_-8px_rgba(79,70,229,.65),inset_0_1px_0_rgba(255,255,255,.2)] active:translate-y-[1px] active:shadow-none"
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
              <div className="h-px flex-1 bg-slate-100" />
              <span className="text-[11px] font-medium uppercase tracking-widest text-slate-400">or</span>
              <div className="h-px flex-1 bg-slate-100" />
            </div>

            <Button
              type="button"
              onClick={handleMetaClick}
              variant="outline"
              className="group h-11 w-full gap-2.5 border-slate-200 bg-white text-[13.5px] font-medium text-slate-700 transition-all hover:border-slate-300 hover:bg-slate-50 hover:shadow-sm"
              disabled={metaLoading}
            >
              {metaLoading ? (
                <>
                  <Loader2 className="size-4 animate-spin" />
                  Connecting to Meta...
                </>
              ) : (
                <>
                  <span className="relative flex size-4 items-center justify-center overflow-hidden rounded-sm bg-gradient-to-br from-pink-600 via-orange-500 to-amber-300">
                    <span className="absolute inset-0 bg-white/20 opacity-0 transition-opacity group-hover:opacity-100" />
                  </span>
                  Continue with Meta Business
                </>
              )}
            </Button>
          </form>

          <div className="mt-8 text-[12.5px] text-slate-500">
            Don&apos;t have an account?{' '}
            <a
              href="mailto:hello@pasukhi.com"
              className="font-medium text-indigo-600 transition-colors hover:text-indigo-700 hover:underline"
            >
              Contact sales
            </a>
          </div>
        </div>
      </div>

      {/* Right side - Abstract Visual Panel */}
      <div className="relative hidden flex-1 overflow-hidden bg-[#0a0f1c] lg:block">
        {/* Dynamic Glows */}
        <div className="absolute -left-1/4 -top-1/4 size-[800px] rounded-full bg-indigo-600/20 blur-[120px]" />
        <div className="absolute -bottom-1/4 -right-1/4 size-[800px] rounded-full bg-amber-500/10 blur-[120px]" />

        {/* Noise Texture Overlay for Premium Feel */}
        <div className="absolute inset-0 bg-[url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI0IiBoZWlnaHQ9IjQiPgoJPHJlY3Qgd2lkdGg9IjQiIGhlaWdodD0iNCIgZmlsbD0iI2ZmZiIgZmlsbC1vcGFjaXR5PSIwLjA1Ii8+Cjwvc3ZnPg==')] opacity-20 mix-blend-overlay" />

        <div className="relative z-10 flex h-full flex-col justify-between p-12 xl:p-16">
          <div className="flex items-center gap-2.5 text-white">
            <div className="flex size-9 items-center justify-center rounded-xl bg-primary shadow-lg shadow-indigo-500/30">
              <PasukhiMark size={18} />
            </div>
            <span className="text-[18px] font-semibold tracking-tight">Pasukhi</span>
          </div>

          <div className="my-auto max-w-[520px]">
            {/* Abstract Floating UI Elements */}
            <div className="mb-14 flex flex-col gap-6">
              <div className="flex w-fit items-center gap-4 rounded-2xl border border-white/10 bg-white/5 p-4 pr-16 shadow-2xl backdrop-blur-md">
                <div className="flex size-11 items-center justify-center rounded-full bg-indigo-500/20 text-indigo-300">
                  <MessageSquare className="size-5" />
                </div>
                <div className="space-y-2.5">
                  <div className="h-2 w-28 rounded-full bg-white/20" />
                  <div className="h-2 w-40 rounded-full bg-white/10" />
                </div>
              </div>

              <div className="flex w-fit translate-x-12 items-center gap-4 rounded-2xl border border-emerald-500/20 bg-emerald-500/10 p-4 pl-16 shadow-2xl backdrop-blur-md">
                <div className="space-y-2.5 text-right">
                  <div className="flex items-center justify-end gap-1.5">
                    <span className="text-[11px] font-semibold uppercase tracking-wider text-emerald-400">Resolved by AI</span>
                    <CheckCircle2 className="size-3.5 text-emerald-400" />
                  </div>
                  <div className="h-2 w-48 rounded-full bg-emerald-400/20" />
                </div>
                <div className="flex size-11 items-center justify-center rounded-full bg-emerald-500/20 text-emerald-400">
                  <Bot className="size-5" />
                </div>
              </div>
            </div>

            <h2 className="text-4xl font-semibold leading-[1.1] tracking-tight text-white xl:text-[44px]">
              Automate your <br />
              <span className="bg-gradient-to-r from-indigo-400 via-indigo-300 to-amber-200 bg-clip-text text-transparent">
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
  )
}
