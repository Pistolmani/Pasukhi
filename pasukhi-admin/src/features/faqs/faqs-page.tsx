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
import { useCrudPage } from '../../hooks/useCrudPage'
import { optionalString } from '../../lib/form-utils'
import { faqSchema } from '../../schemas/faq-schemas'
import type { SaveFaqItemRequest } from '../../types/faq'

const emptyForm: SaveFaqItemRequest = {
  question: '',
  answer: '',
  keywords: null,
  isActive: true,
  sortOrder: 0,
}

export function FaqsPage() {
  const crud = useCrudPage({
    queryKey: ['faqs'],
    api: faqsApi,
    schema: faqSchema,
    emptyForm,
    toForm: (faq) => ({
      question: faq.question,
      answer: faq.answer,
      keywords: faq.keywords,
      isActive: faq.isActive,
      sortOrder: faq.sortOrder,
    }),
    preparePayload: (form) => ({
      ...form,
      question: form.question.trim(),
      answer: form.answer.trim(),
      keywords: optionalString(form.keywords),
    }),
    entityLabel: 'FAQ',
  })

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">FAQs</h1>
          <p className="text-muted-foreground text-sm">Reusable answers for deterministic matching.</p>
        </div>
        <Button type="button" onClick={crud.openCreate}>Add FAQ</Button>
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
            {crud.query.data?.map((faq) => (
              <TableRow key={faq.id}>
                <TableCell className="max-w-md whitespace-normal">
                  <div className="font-medium">{faq.question}</div>
                  <div className="text-muted-foreground mt-1 line-clamp-2 text-xs">{faq.answer}</div>
                </TableCell>
                <TableCell className="max-w-xs truncate">{faq.keywords || 'None'}</TableCell>
                <TableCell>{faq.matchCount}</TableCell>
                <TableCell>{faq.isActive ? 'Active' : 'Paused'}</TableCell>
                <TableCell className="space-x-2 text-right">
                  <Button type="button" variant="outline" onClick={() => crud.openEdit(faq)}>Edit</Button>
                  <Button
                    type="button"
                    variant="destructive"
                    disabled={crud.deleteMutation.isPending}
                    onClick={() => crud.deleteMutation.mutate(faq.id)}
                  >
                    Delete
                  </Button>
                </TableCell>
              </TableRow>
            ))}
            {crud.query.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-muted-foreground py-8 text-center">
                  No FAQs yet.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      <Dialog open={crud.open} onOpenChange={(next) => (next ? crud.setOpen(true) : crud.closeDialog())}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{crud.editing ? 'Edit FAQ' : 'Add FAQ'}</DialogTitle>
            <DialogDescription>Keywords can be comma, semicolon, pipe, or newline separated.</DialogDescription>
          </DialogHeader>
          <form className="space-y-4" onSubmit={crud.onSubmit}>
            <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
              <div className="space-y-2">
                <Label htmlFor="question">Question</Label>
                <Input
                  id="question"
                  value={crud.form.question}
                  onChange={(e) => crud.setForm({ ...crud.form, question: e.target.value })}
                  required
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="sortOrder">Sort order</Label>
                <Input
                  id="sortOrder"
                  type="number"
                  min={0}
                  value={crud.form.sortOrder}
                  onChange={(e) => crud.setForm({ ...crud.form, sortOrder: Number(e.target.value) })}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="answer">Answer</Label>
              <Textarea
                id="answer"
                className="min-h-28"
                value={crud.form.answer}
                onChange={(e) => crud.setForm({ ...crud.form, answer: e.target.value })}
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="keywords">Keywords</Label>
              <Textarea
                id="keywords"
                value={crud.form.keywords ?? ''}
                onChange={(e) => crud.setForm({ ...crud.form, keywords: e.target.value })}
                placeholder="ფასი, price, cost"
              />
            </div>

            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={crud.form.isActive}
                onCheckedChange={(checked) => crud.setForm({ ...crud.form, isActive: checked === true })}
              />
              Active
            </label>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={crud.closeDialog}>Cancel</Button>
              <Button type="submit" disabled={crud.saveMutation.isPending}>
                {crud.editing ? 'Save FAQ' : 'Create FAQ'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  )
}
