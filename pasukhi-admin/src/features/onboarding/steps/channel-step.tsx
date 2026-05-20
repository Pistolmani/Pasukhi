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
          <p className="text-[12px] text-slate-400">
            The numeric ID of your Instagram account or Facebook Page (found in Business Settings).
          </p>
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
          <p className="text-[12px] text-slate-400">
            Display name or handle — only used inside Pasukhi for your reference.
          </p>
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
          <p className="text-[12px] text-slate-400">
            A long-lived Page Access Token from your Meta app (valid 60 days). Must have the{' '}
            <code className="rounded bg-slate-100 px-1">pages_messaging</code> permission.
          </p>
          <Input
            type="password"
            placeholder="Paste your long-lived page access token"
            value={form.accessToken}
            onChange={(e) => setForm({ ...form, accessToken: e.target.value })}
            className="h-11 border-slate-200 bg-white text-[14px]"
          />
        </div>

        <div className="space-y-1.5">
          <Label className="text-[12.5px] font-medium text-slate-700">
            Verify token <span className="font-normal text-slate-400">(optional)</span>
          </Label>
          <p className="text-[12px] text-slate-400">
            A random string you choose and paste into both Meta&apos;s webhook settings and here — so Meta can verify the connection.
          </p>
          <Input
            placeholder="e.g. pasukhi_verify_abc123"
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
          <div className="space-y-4 rounded-xl bg-slate-50 px-4 py-4 text-[13px] leading-relaxed text-slate-600 ring-1 ring-slate-200/60">
            <div>
              <p className="mb-2 font-semibold text-slate-800">Step 1 — Create a Meta App</p>
              <ol className="list-decimal space-y-1 pl-4">
                <li>
                  Go to{' '}
                  <a
                    href="https://developers.facebook.com/apps"
                    target="_blank"
                    rel="noreferrer"
                    className="font-medium text-indigo-600 hover:underline"
                  >
                    developers.facebook.com/apps
                  </a>
                </li>
                <li>Click <strong>Create App</strong> → choose <strong>Business</strong> type</li>
                <li>Give it a name (e.g. "Pasukhi Bot") and link it to your Business portfolio</li>
              </ol>
            </div>

            <div>
              <p className="mb-2 font-semibold text-slate-800">Step 2 — Add Messenger or Instagram product</p>
              <ol className="list-decimal space-y-1 pl-4">
                <li>Inside your app, click <strong>Add Product</strong> in the left sidebar</li>
                <li>
                  Find <strong>Messenger</strong> (for Facebook Pages) or{' '}
                  <strong>Instagram</strong> (for Instagram accounts) and click <strong>Set Up</strong>
                </li>
                <li>Connect your Facebook Page or Instagram Business account when prompted</li>
              </ol>
            </div>

            <div>
              <p className="mb-2 font-semibold text-slate-800">Step 3 — Get the External Account ID</p>
              <ol className="list-decimal space-y-1 pl-4">
                <li>
                  For <strong>Instagram</strong>: go to{' '}
                  <a
                    href="https://business.facebook.com/settings/instagram-accounts"
                    target="_blank"
                    rel="noreferrer"
                    className="font-medium text-indigo-600 hover:underline"
                  >
                    Business Settings → Instagram Accounts
                  </a>
                  , click your account — the ID is in the URL (a long number)
                </li>
                <li>
                  For <strong>Messenger</strong>: go to{' '}
                  <a
                    href="https://business.facebook.com/settings/pages"
                    target="_blank"
                    rel="noreferrer"
                    className="font-medium text-indigo-600 hover:underline"
                  >
                    Business Settings → Pages
                  </a>
                  , click your Page → the Page ID is shown below the page name
                </li>
              </ol>
            </div>

            <div>
              <p className="mb-2 font-semibold text-slate-800">Step 4 — Generate the Access Token</p>
              <ol className="list-decimal space-y-1 pl-4">
                <li>In your Meta app, go to <strong>Messenger → Settings</strong> (or <strong>Instagram → Settings</strong>)</li>
                <li>Scroll to <strong>Access Tokens</strong> and click <strong>Generate Token</strong> next to your page/account</li>
                <li>
                  Copy the token — this is a <em>short-lived</em> token (expires in 1 hour). To make it permanent, use the{' '}
                  <a
                    href="https://developers.facebook.com/tools/explorer/"
                    target="_blank"
                    rel="noreferrer"
                    className="font-medium text-indigo-600 hover:underline"
                  >
                    Graph API Explorer
                  </a>{' '}
                  → click <strong>Generate Long-Lived Token</strong> (valid for 60 days)
                </li>
                <li>Make sure the token has the <code className="rounded bg-slate-200 px-1">pages_messaging</code> permission</li>
              </ol>
            </div>

            <div>
              <p className="mb-2 font-semibold text-slate-800">Step 5 — Set the Verify Token</p>
              <ol className="list-decimal space-y-1 pl-4">
                <li>Make up any random string, e.g. <code className="rounded bg-slate-200 px-1">pasukhi_verify_123</code></li>
                <li>Paste that same string here and in the <strong>Webhook → Verify Token</strong> field in your Meta app</li>
                <li>The Webhooks page in Pasukhi (sidebar → Webhooks) shows the full webhook URL to paste into Meta</li>
              </ol>
            </div>

            <p className="border-t border-slate-200 pt-3 text-[12px] text-slate-400">
              Need help? Email{' '}
              <a href="mailto:hello@pasukhi.com" className="text-indigo-600 hover:underline">
                hello@pasukhi.com
              </a>{' '}
              and we'll walk you through it.
            </p>
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
