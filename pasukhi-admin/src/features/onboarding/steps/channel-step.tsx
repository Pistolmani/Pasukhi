import { ChevronDown, ChevronRight, Loader2, MessageSquare } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { channelsApi } from '../../../api/channels'
import { Button } from '../../../components/ui/button'
import { Input } from '../../../components/ui/input'
import { Label } from '../../../components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../components/ui/select'
import { channelTypeLabels, channelTypes, type ChannelType } from '../../../types/channel'

type ChannelFormState = {
  channelType: ChannelType
  externalAccountId: string
  externalAccountName: string
  accessToken: string
  verifyToken: string
}

const INITIAL_FORM: ChannelFormState = {
  channelType: 0,
  externalAccountId: '',
  externalAccountName: '',
  accessToken: '',
  verifyToken: '',
}

export function ChannelStep({ onNext }: { onNext: () => void }) {
  const [form, setForm] = useState<ChannelFormState>(INITIAL_FORM)
  const [submitting, setSubmitting] = useState(false)
  const [showHelp, setShowHelp] = useState(false)

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!form.externalAccountId || !form.accessToken) {
      toast.error('Account ID and access token are required')
      return
    }
    setSubmitting(true)
    try {
      await channelsApi.create({
        channelType: form.channelType,
        externalAccountId: form.externalAccountId,
        externalAccountName: form.externalAccountName || null,
        accessToken: form.accessToken,
        verifyToken: form.verifyToken || null,
        isActive: true,
      })
      toast.success('Channel connected')
      onNext()
    } catch {
      toast.error('Failed to connect channel')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div>
      <div className="mb-8">
        <div className="mb-4 flex size-12 items-center justify-center rounded-2xl bg-indigo-500/15 text-indigo-500">
          <MessageSquare className="size-6" />
        </div>
        <p className="mb-1 text-[12px] font-semibold uppercase tracking-widest text-slate-400">
          Step 3 of 7
        </p>
        <h2 className="text-[26px] font-semibold tracking-tight text-slate-950">Connect a channel</h2>
        <p className="mt-2 text-[14px] text-slate-500">
          Link your Instagram, Messenger, or WhatsApp account so customer messages flow into Pasukhi.
        </p>
      </div>

      <form onSubmit={onSubmit} className="space-y-4">
        <div className="space-y-1.5">
          <Label className="text-[12.5px] font-medium text-slate-700">Channel</Label>
          <Select
            value={String(form.channelType)}
            onValueChange={(value) =>
              setForm({ ...form, channelType: Number(value) as ChannelType })
            }
          >
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {channelTypes.map((value) => (
                <SelectItem key={value} value={String(value)}>
                  {channelTypeLabels[value]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label className="text-[12.5px] font-medium text-slate-700">
            External account ID <span className="text-rose-500">*</span>
          </Label>
          <Input
            placeholder="e.g. 17841401234567890"
            value={form.externalAccountId}
            onChange={(e) => setForm({ ...form, externalAccountId: e.target.value })}
            className="h-11 border-slate-200 bg-white text-[14px]"
          />
        </div>

        <div className="space-y-1.5">
          <Label className="text-[12.5px] font-medium text-slate-700">
            Account name <span className="font-normal text-slate-400">(optional)</span>
          </Label>
          <Input
            placeholder="@yourhandle"
            value={form.externalAccountName}
            onChange={(e) => setForm({ ...form, externalAccountName: e.target.value })}
            className="h-11 border-slate-200 bg-white text-[14px]"
          />
        </div>

        <div className="space-y-1.5">
          <Label className="text-[12.5px] font-medium text-slate-700">
            Access token <span className="text-rose-500">*</span>
          </Label>
          <Input
            type="password"
            placeholder="Long-lived page access token"
            value={form.accessToken}
            onChange={(e) => setForm({ ...form, accessToken: e.target.value })}
            className="h-11 border-slate-200 bg-white text-[14px]"
          />
        </div>

        <div className="space-y-1.5">
          <Label className="text-[12.5px] font-medium text-slate-700">
            Verify token <span className="font-normal text-slate-400">(optional)</span>
          </Label>
          <Input
            placeholder="Webhook verify token"
            value={form.verifyToken}
            onChange={(e) => setForm({ ...form, verifyToken: e.target.value })}
            className="h-11 border-slate-200 bg-white text-[14px]"
          />
        </div>

        <button
          type="button"
          onClick={() => setShowHelp((s) => !s)}
          className="flex w-full items-center justify-between rounded-xl bg-slate-50 px-4 py-3 text-left text-[13px] font-medium text-slate-600 ring-1 ring-slate-200/60 transition-colors hover:bg-slate-100"
        >
          How do I find these values?
          <ChevronDown className={`size-4 transition-transform ${showHelp ? 'rotate-180' : ''}`} />
        </button>
        {showHelp && (
          <div className="rounded-xl bg-slate-50 px-4 py-3 text-[13px] leading-relaxed text-slate-600 ring-1 ring-slate-200/60">
            Open{' '}
            <a
              href="https://business.facebook.com/"
              target="_blank"
              rel="noreferrer"
              className="font-medium text-indigo-600 hover:underline"
            >
              Meta Business Suite
            </a>
            , go to <strong>Settings → Accounts</strong>, pick your Instagram or Facebook Page, and
            copy its ID. The access token comes from your Meta app dashboard → <strong>Generate
            token</strong>. Pick a long-lived page token with the <code>pages_messaging</code>{' '}
            scope.
          </div>
        )}

        <Button
          type="submit"
          size="lg"
          disabled={submitting}
          className="mt-2 h-12 w-full gap-2 text-[15px] font-medium shadow-[0_6px_18px_-8px_rgba(79,70,229,.55)]"
        >
          {submitting ? (
            <>
              <Loader2 className="size-4 animate-spin" />
              Connecting...
            </>
          ) : (
            <>
              Connect channel
              <ChevronRight className="size-4" />
            </>
          )}
        </Button>
      </form>
    </div>
  )
}
