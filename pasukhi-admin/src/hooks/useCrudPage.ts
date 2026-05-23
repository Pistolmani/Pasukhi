import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { useState } from 'react'
import { toast } from 'sonner'
import type { ZodType } from 'zod'
import { firstIssue } from '../lib/form-utils'

interface CrudApi<TItem, TForm> {
  list: () => Promise<TItem[]>
  create: (data: TForm) => Promise<TItem>
  update: (id: string, data: TForm) => Promise<TItem>
  remove: (id: string) => Promise<void>
}

interface UseCrudPageOptions<TItem extends { id: string }, TForm> {
  queryKey: string[]
  api: CrudApi<TItem, TForm>
  schema: ZodType<TForm>
  emptyForm: TForm
  toForm: (item: TItem) => TForm
  preparePayload?: (form: TForm) => TForm
  entityLabel: string
}

export function useCrudPage<TItem extends { id: string }, TForm>({
  queryKey,
  api,
  schema,
  emptyForm,
  toForm,
  preparePayload,
  entityLabel,
}: UseCrudPageOptions<TItem, TForm>) {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<TItem | null>(null)
  const [form, setForm] = useState<TForm>({ ...emptyForm })

  const query = useQuery({ queryKey, queryFn: api.list })

  const saveMutation = useMutation({
    mutationFn: (payload: TForm) =>
      editing ? api.update(editing.id, payload) : api.create(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey })
      toast.success(editing ? `${entityLabel} updated` : `${entityLabel} created`)
      closeDialog()
    },
    onError: () => toast.error(`Could not save ${entityLabel.toLowerCase()}`),
  })

  const deleteMutation = useMutation({
    mutationFn: api.remove,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey })
      toast.success(`${entityLabel} deleted`)
    },
    onError: () => toast.error(`Could not delete ${entityLabel.toLowerCase()}`),
  })

  const openCreate = () => {
    setEditing(null)
    setForm({ ...emptyForm })
    setOpen(true)
  }

  const openEdit = (item: TItem) => {
    setEditing(item)
    setForm(toForm(item))
    setOpen(true)
  }

  const closeDialog = () => {
    setOpen(false)
    setEditing(null)
    setForm({ ...emptyForm })
  }

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const payload = preparePayload ? preparePayload(form) : form
    const parsed = schema.safeParse(payload)
    if (!parsed.success) {
      toast.error(firstIssue(parsed.error))
      return
    }
    saveMutation.mutate(parsed.data)
  }

  return {
    query,
    open,
    setOpen,
    editing,
    form,
    setForm,
    openCreate,
    openEdit,
    closeDialog,
    onSubmit,
    saveMutation,
    deleteMutation,
  }
}
