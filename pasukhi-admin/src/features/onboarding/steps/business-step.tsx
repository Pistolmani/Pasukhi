import { zodResolver } from '@hookform/resolvers/zod'
import { Bot, ChevronRight, Loader2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { authApi } from '../../../api/auth'
import { Button } from '../../../components/ui/button'
import { Input } from '../../../components/ui/input'
import { Label } from '../../../components/ui/label'
import { useAuthStore } from '../../../stores/auth-store'

const businessSchema = z.object({
  name: z.string().min(2, 'Business name must be at least 2 characters'),
  description: z.string().optional(),
})
type BusinessFormData = z.infer<typeof businessSchema>

export function BusinessStep({ onNext }: { onNext: () => void }) {
  const setAuth = useAuthStore((state) => state.setAuth)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<BusinessFormData>({ resolver: zodResolver(businessSchema) })

  const onSubmit = async (data: BusinessFormData) => {
    const result = await authApi.setupBusiness(data.name, data.description)
    setAuth(result.user, result.accessToken)
    onNext()
  }

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

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
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
            Description <span className="font-normal text-slate-400">(optional)</span>
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
