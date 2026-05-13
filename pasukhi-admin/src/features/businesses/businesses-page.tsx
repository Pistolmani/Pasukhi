import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import type { FormEvent } from 'react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { Building2, UserPlus } from 'lucide-react'
import { adminUsersApi } from '../../api/admin-users'
import { businessesApi } from '../../api/businesses'
import { Button } from '../../components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../components/ui/dialog'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'
import { Textarea } from '../../components/ui/textarea'
import type { CreateAdminUserRequest } from '../../types/admin-user'
import type { Business, CreateBusinessRequest } from '../../types/business'

const emptyBusinessForm: CreateBusinessRequest = {
  name: '',
  slug: '',
  description: null,
  logoUrl: null,
}

const emptyOperatorForm: Omit<CreateAdminUserRequest, 'businessId'> = {
  email: '',
  firstName: '',
  lastName: '',
  password: '',
}

export function BusinessesPage() {
  const queryClient = useQueryClient()
  const [businessDialogOpen, setBusinessDialogOpen] = useState(false)
  const [operatorDialogOpen, setOperatorDialogOpen] = useState(false)
  const [selectedBusiness, setSelectedBusiness] = useState<Business | null>(null)
  const [businessForm, setBusinessForm] = useState<CreateBusinessRequest>({ ...emptyBusinessForm })
  const [operatorForm, setOperatorForm] = useState<Omit<CreateAdminUserRequest, 'businessId'>>({ ...emptyOperatorForm })

  const businessesQuery = useQuery({
    queryKey: ['businesses'],
    queryFn: businessesApi.list,
  })

  const usersQuery = useQuery({
    queryKey: ['admin-users'],
    queryFn: () => adminUsersApi.list(),
  })

  const operatorCounts = useMemo(() => {
    const counts = new Map<string, number>()
    for (const user of usersQuery.data ?? []) {
      if (user.businessId && user.role === 'Operator') {
        counts.set(user.businessId, (counts.get(user.businessId) ?? 0) + 1)
      }
    }
    return counts
  }, [usersQuery.data])

  const createBusinessMutation = useMutation({
    mutationFn: businessesApi.create,
    onSuccess: async (business) => {
      await queryClient.invalidateQueries({ queryKey: ['businesses'] })
      await queryClient.invalidateQueries({ queryKey: ['sidebar', 'businesses'] })
      toast.success('Tenant created')
      setBusinessDialogOpen(false)
      setBusinessForm({ ...emptyBusinessForm })
      openOperatorDialog(business)
    },
    onError: (error) => toast.error(errorMessage(error, 'Could not create tenant')),
  })

  const createOperatorMutation = useMutation({
    mutationFn: adminUsersApi.create,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-users'] })
      toast.success('Operator created')
      setOperatorDialogOpen(false)
      setSelectedBusiness(null)
      setOperatorForm({ ...emptyOperatorForm })
    },
    onError: (error) => toast.error(errorMessage(error, 'Could not create operator')),
  })

  const businesses = businessesQuery.data ?? []

  const updateBusinessName = (name: string) => {
    setBusinessForm((current) => ({
      ...current,
      name,
      slug: current.slug || slugify(name),
    }))
  }

  const submitBusiness = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const payload: CreateBusinessRequest = {
      name: businessForm.name.trim(),
      slug: businessForm.slug.trim(),
      description: normalizeOptional(businessForm.description),
      logoUrl: normalizeOptional(businessForm.logoUrl),
    }

    if (!payload.name || !payload.slug) {
      toast.error('Tenant name and slug are required')
      return
    }

    createBusinessMutation.mutate(payload)
  }

  const submitOperator = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!selectedBusiness) return

    const payload: CreateAdminUserRequest = {
      email: operatorForm.email.trim(),
      firstName: operatorForm.firstName.trim(),
      lastName: operatorForm.lastName.trim(),
      password: operatorForm.password,
      businessId: selectedBusiness.id,
    }

    if (!payload.email || !payload.firstName || !payload.lastName || !payload.password) {
      toast.error('All operator fields are required')
      return
    }

    createOperatorMutation.mutate(payload)
  }

  const openOperatorDialog = (business: Business) => {
    setSelectedBusiness(business)
    setOperatorForm({ ...emptyOperatorForm })
    setOperatorDialogOpen(true)
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Businesses</h1>
          <p className="text-muted-foreground text-sm">
            Create tenants and attach business operators to them.
          </p>
        </div>
        <Button type="button" onClick={() => setBusinessDialogOpen(true)}>
          <Building2 className="size-4" />
          Add tenant
        </Button>
      </div>

      <div className="overflow-hidden rounded-md border bg-white">
        <table className="w-full text-left text-[13px]">
          <thead className="border-b border-border bg-muted/50 text-[11px] uppercase tracking-[0.14em] text-slate-500">
            <tr>
              <th className="px-5 py-3 font-semibold">Name</th>
              <th className="px-5 py-3 font-semibold">Slug</th>
              <th className="px-5 py-3 font-semibold">Description</th>
              <th className="px-5 py-3 font-semibold">Operators</th>
              <th className="px-5 py-3 font-semibold">Status</th>
              <th className="px-5 py-3 text-right font-semibold">Actions</th>
            </tr>
          </thead>
          <tbody>
            {businesses.map((business) => (
              <tr key={business.id} className="border-b border-border/70 last:border-0">
                <td className="px-5 py-4">
                  <div className="flex items-center gap-3">
                    <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-indigo-50 text-indigo-700">
                      <Building2 className="size-4" />
                    </div>
                    <div className="min-w-0">
                      <div className="truncate font-semibold text-slate-950">{business.name}</div>
                      <div className="truncate text-[11.5px] text-slate-500">{business.id}</div>
                    </div>
                  </div>
                </td>
                <td className="px-5 py-4 text-slate-700">{business.slug}</td>
                <td className="max-w-sm px-5 py-4 text-slate-600">
                  <span className="line-clamp-2">{business.description || 'No description'}</span>
                </td>
                <td className="px-5 py-4 text-slate-700">{operatorCounts.get(business.id) ?? 0}</td>
                <td className="px-5 py-4">
                  <span
                    className={
                      business.isActive
                        ? 'rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700'
                        : 'rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-600'
                    }
                  >
                    {business.isActive ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td className="px-5 py-4 text-right">
                  <Button type="button" variant="outline" onClick={() => openOperatorDialog(business)}>
                    <UserPlus className="size-4" />
                    Add operator
                  </Button>
                </td>
              </tr>
            ))}
            {!businessesQuery.isLoading && businesses.length === 0 && (
              <tr>
                <td colSpan={6} className="px-5 py-10 text-center text-slate-500">
                  No tenants found.
                </td>
              </tr>
            )}
            {businessesQuery.isLoading && (
              <tr>
                <td colSpan={6} className="px-5 py-10 text-center text-slate-500">
                  Loading tenants...
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Dialog open={businessDialogOpen} onOpenChange={setBusinessDialogOpen}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Add tenant</DialogTitle>
            <DialogDescription>
              Create a business workspace. Operators, FAQs, rules, channels, and conversations belong to this tenant.
            </DialogDescription>
          </DialogHeader>
          <form className="space-y-4" onSubmit={submitBusiness}>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="businessName">Name</Label>
                <Input
                  id="businessName"
                  value={businessForm.name}
                  onChange={(event) => updateBusinessName(event.target.value)}
                  required
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="businessSlug">Slug</Label>
                <Input
                  id="businessSlug"
                  value={businessForm.slug}
                  onChange={(event) => setBusinessForm({ ...businessForm, slug: slugify(event.target.value) })}
                  placeholder="my-business"
                  required
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="businessDescription">Description</Label>
              <Textarea
                id="businessDescription"
                value={businessForm.description ?? ''}
                onChange={(event) => setBusinessForm({ ...businessForm, description: event.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="businessLogo">Logo URL</Label>
              <Input
                id="businessLogo"
                type="url"
                value={businessForm.logoUrl ?? ''}
                onChange={(event) => setBusinessForm({ ...businessForm, logoUrl: event.target.value })}
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setBusinessDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={createBusinessMutation.isPending}>
                {createBusinessMutation.isPending ? 'Creating...' : 'Create tenant'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={operatorDialogOpen} onOpenChange={setOperatorDialogOpen}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Add operator</DialogTitle>
            <DialogDescription>
              {selectedBusiness ? `Create a business-side user for ${selectedBusiness.name}.` : 'Create a business-side user.'}
            </DialogDescription>
          </DialogHeader>
          <form className="space-y-4" onSubmit={submitOperator}>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="operatorFirstName">First name</Label>
                <Input
                  id="operatorFirstName"
                  value={operatorForm.firstName}
                  onChange={(event) => setOperatorForm({ ...operatorForm, firstName: event.target.value })}
                  required
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="operatorLastName">Last name</Label>
                <Input
                  id="operatorLastName"
                  value={operatorForm.lastName}
                  onChange={(event) => setOperatorForm({ ...operatorForm, lastName: event.target.value })}
                  required
                />
              </div>
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="operatorEmail">Email</Label>
                <Input
                  id="operatorEmail"
                  type="email"
                  value={operatorForm.email}
                  onChange={(event) => setOperatorForm({ ...operatorForm, email: event.target.value })}
                  required
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="operatorPassword">Temporary password</Label>
                <Input
                  id="operatorPassword"
                  type="password"
                  value={operatorForm.password}
                  onChange={(event) => setOperatorForm({ ...operatorForm, password: event.target.value })}
                  required
                />
              </div>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setOperatorDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={createOperatorMutation.isPending}>
                {createOperatorMutation.isPending ? 'Creating...' : 'Create operator'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function normalizeOptional(value: string | null) {
  return value?.trim() ? value.trim() : null
}

function slugify(value: string) {
  return value
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

function errorMessage(error: unknown, fallback: string) {
  if (axios.isAxiosError(error)) {
    const message = (error.response?.data as { error?: string } | undefined)?.error
    return message || fallback
  }

  return fallback
}
