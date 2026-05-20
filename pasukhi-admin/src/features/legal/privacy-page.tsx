import { ArrowLeft } from 'lucide-react'
import { Link } from 'react-router-dom'
import { PasukhiMark } from '../../components/brand/pasukhi-mark'

export function PrivacyPage() {
  return (
    <div className="min-h-screen w-full bg-stone-50 px-6 py-12">
      <div className="mx-auto w-full max-w-[720px]">
        <Link
          to="/login"
          className="inline-flex items-center gap-1.5 text-[13px] font-medium text-indigo-600 transition-colors hover:text-indigo-700 hover:underline"
        >
          <ArrowLeft className="size-3.5" />
          Back to sign in
        </Link>

        <div className="mt-8 flex items-center gap-2.5 text-slate-900">
          <div className="flex size-9 items-center justify-center rounded-xl bg-primary text-white">
            <PasukhiMark size={16} />
          </div>
          <span className="text-[18px] font-semibold tracking-tight">Pasukhi</span>
        </div>

        <h1 className="mt-6 text-[32px] font-semibold tracking-tight text-slate-950">Privacy Policy</h1>
        <p className="mt-2 text-[14px] text-slate-500">Last updated: May 2026</p>

        <div className="mt-8 space-y-5 text-[14.5px] leading-relaxed text-slate-700">
          <p>
            This page describes how Pasukhi collects, uses, and protects information when you use our admin
            dashboard and automation services.
          </p>

          <h2 className="pt-4 text-[18px] font-semibold tracking-tight text-slate-950">Information we collect</h2>
          <p>
            We collect account information you provide (name, email, business details), conversation data your
            integrated channels send to our service, and basic usage analytics.
          </p>

          <h2 className="pt-4 text-[18px] font-semibold tracking-tight text-slate-950">How we use it</h2>
          <p>
            Your data is used to operate the service, train your bot on your provided knowledge base, and
            deliver responses to your customers. We do not sell your data.
          </p>

          <h2 className="pt-4 text-[18px] font-semibold tracking-tight text-slate-950">Contact</h2>
          <p>
            Questions? Reach out at{' '}
            <a
              href="mailto:hello@pasukhi.com"
              className="font-medium text-indigo-600 transition-colors hover:text-indigo-700 hover:underline"
            >
              hello@pasukhi.com
            </a>
            .
          </p>
        </div>
      </div>
    </div>
  )
}
