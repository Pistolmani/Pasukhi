import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { useState } from 'react'
import { toast } from 'sonner'
import { faqsApi } from '../../api/faqs'
import { Button } from '../../components/ui/button'
import { Checkbox } from '../../components/ui/checkbox'
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../components/ui/table'
import { Textarea } from '../../components/ui/textarea'
import { faqSchema } from '../../schemas/faq-schemas'
import { firstIssue, optionalString } from '../../lib/form-utils'
import type { FaqItem, SaveFaqItemRequest } from '../../types/faq'

const emptyForm: SaveFaqItemRequest = {
  question: '',
  answer: '',
  keywords: null,
  isActive: true,
  sortOrder: 0,
}

export function FaqsPage() {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<FaqItem | null>(null)
  const [form, setForm] = useState<SaveFaqItemRequest>({ ...emptyForm })

  const faqsQuery = useQuery({
    queryKey: ['faqs'],
    queryFn: faqsApi.list,
  })

  const saveMutation = useMutation({
    mutationFn: (payload: SaveFaqItemRequest) =>
      editing ? faqsApi.update(editing.id, payload) : faqsApi.create(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['faqs'] })
      toast.success(editing ? 'FAQ updated' : 'FAQ created')
      closeDialog()
    },
    onError: () => toast.error('Could not save FAQ'),
  })

  const deleteMutation = useMutation({
    mutationFn: faqsApi.remove,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['faqs'] })
      toast.success('FAQ deleted')
    },
    onError: () => toast.error('Could not delete FAQ'),
  })

  const openCreate = () => {
    setEditing(null)
    setForm({ ...emptyForm })
    setOpen(true)
  }

  const openEdit = (faq: FaqItem) => {
    setEditing(faq)
    setForm({
      question: faq.question,
      answer: faq.answer,
      keywords: faq.keywords,
      isActive: faq.isActive,
      sortOrder: faq.sortOrder,
    })
    setOpen(true)
  }

  const closeDialog = () => {
    setOpen(false)
    setEditing(null)
    setForm({ ...emptyForm })
  }

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const payload = {
      ...form,
      question: form.question.trim(),
      answer: form.answer.trim(),
      keywords: optionalString(form.keywords),
    }

    const parsed = faqSchema.safeParse(payload)
    if (!parsed.success) {
      toast.error(firstIssue(parsed.error))
      return
    }

    saveMutation.mutate(parsed.data)
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">FAQs</h1>
          <p className="text-muted-foreground text-sm">Reusable answers for deterministic matching.</p>
        </div>
        <Button type="button" onClick={openCreate}>Add FAQ</Button>
      </div>

      <div className="overflow-hidden rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Question</TableHead>
              <TableHead>Keywords</TableHead>
              <TableHead>Matches</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {faqsQuery.data?.map((faq) => (
              <TableRow key={faq.id}>
                <TableCell className="max-w-md whitespace-normal">
                  <div className="font-medium">{faq.question}</div>
                  <div className="text-muted-foreground mt-1 line-clamp-2 text-xs">{faq.answer}</div>
                </TableCell>
                <TableCell className="max-w-xs truncate">{faq.keywords || 'None'}</TableCell>
                <TableCell>{faq.matchCount}</TableCell>
                <TableCell>{faq.isActive ? 'Active' : 'Paused'}</TableCell>
                <TableCell className="space-x-2 text-right">
                  <Button type="button" variant="outline" onClick={() => openEdit(faq)}>Edit</Button>
                  <Button
                    type="button"
                    variant="destructive"
                    disabled={deleteMutation.isPending}
                    onClick={() => deleteMutation.mutate(faq.id)}
                  >
                    Delete
                  </Button>
                </TableCell>
              </TableRow>
            ))}
            {faqsQuery.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-muted-foreground py-8 text-center">
                  No FAQs yet.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      <Dialog open={open} onOpenChange={(nextOpen) => (nextOpen ? setOpen(true) : closeDialog())}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit FAQ' : 'Add FAQ'}</DialogTitle>
            <DialogDescription>Keywords can be comma, semicolon, pipe, or newline separated.</DialogDescription>
          </DialogHeader>
          <form className="space-y-4" onSubmit={onSubmit}>
            <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
              <div className="space-y-2">
                <Label htmlFor="question">Question</Label>
                <Input
                  id="question"
                  value={form.question}
                  onChange={(event) => setForm({ ...form, question: event.target.value })}
                  required
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="sortOrder">Sort order</Label>
                <Input
                  id="sortOrder"
                  type="number"
                  min={0}
                  value={form.sortOrder}
                  onChange={(event) => setForm({ ...form, sortOrder: Number(event.target.value) })}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="answer">Answer</Label>
              <Textarea
                id="answer"
                className="min-h-28"
                value={form.answer}
                onChange={(event) => setForm({ ...form, answer: event.target.value })}
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="keywords">Keywords</Label>
              <Textarea
                id="keywords"
                value={form.keywords ?? ''}
                onChange={(event) => setForm({ ...form, keywords: event.target.value })}
                placeholder="ფასი, price, cost"
              />
            </div>

            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={form.isActive}
                onCheckedChange={(checked) => setForm({ ...form, isActive: checked === true })}
              />
              Active
            </label>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={closeDialog}>Cancel</Button>
              <Button type="submit" disabled={saveMutation.isPending}>
                {editing ? 'Save FAQ' : 'Create FAQ'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  )
}
