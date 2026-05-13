type PasukhiMarkProps = {
  className?: string
  size?: number
}

export function PasukhiMark({ className, size = 22 }: PasukhiMarkProps) {
  return (
    <svg
      className={className}
      width={size * 1.6}
      height={size}
      viewBox="0 0 64 40"
      fill="none"
      aria-hidden="true"
      xmlns="http://www.w3.org/2000/svg"
    >
      <path d="M16 8 6 20l10 12" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
      <path d="m48 8 10 12-10 12" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="24" cy="20" r="2.6" fill="currentColor" />
      <circle cx="32" cy="20" r="2.6" fill="currentColor" />
      <circle cx="40" cy="20" r="2.6" fill="currentColor" />
    </svg>
  )
}
