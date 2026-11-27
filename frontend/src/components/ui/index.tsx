import {
  useId,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from 'react'
import { Link, type LinkProps } from 'react-router'
import styles from './ui.module.css'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger'

function buttonClass(variant: Variant, small?: boolean, iconOnly?: boolean) {
  return [styles.button, styles[variant], small && styles.small, iconOnly && styles.iconOnly]
    .filter(Boolean)
    .join(' ')
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  small?: boolean
  iconOnly?: boolean
}

export function Button({
  variant = 'secondary',
  small,
  iconOnly,
  type = 'button',
  className,
  ...rest
}: ButtonProps) {
  return (
    <button type={type} className={[buttonClass(variant, small, iconOnly), className].join(' ')} {...rest} />
  )
}

/** A button that opens the file picker. */
export function FileButton({
  children,
  accept,
  onFile,
  variant = 'secondary',
  small,
  disabled,
}: {
  children: ReactNode
  accept?: string
  onFile: (file: File) => void
  variant?: Variant
  small?: boolean
  disabled?: boolean
}) {
  return (
    <label className={buttonClass(variant, small)} aria-disabled={disabled}>
      {children}
      <input
        type="file"
        className="visually-hidden"
        accept={accept}
        disabled={disabled}
        onChange={(e) => {
          const file = e.target.files?.[0]
          if (file) onFile(file)
          e.target.value = ''
        }}
      />
    </label>
  )
}

interface ButtonLinkProps extends LinkProps {
  variant?: Variant
  small?: boolean
}

export function ButtonLink({ variant = 'secondary', small, className, ...rest }: ButtonLinkProps) {
  return <Link className={[buttonClass(variant, small), className].join(' ')} {...rest} />
}

export function Card({
  children,
  className,
  as: Tag = 'section',
}: {
  children: ReactNode
  className?: string
  as?: 'section' | 'div' | 'li' | 'article'
}) {
  return <Tag className={[styles.card, className].filter(Boolean).join(' ')}>{children}</Tag>
}

interface FieldShellProps {
  label: string
  hint?: ReactNode
  children: (id: string, hintId?: string) => ReactNode
}

function FieldShell({ label, hint, children }: FieldShellProps) {
  const id = useId()
  const hintId = hint ? `${id}-hint` : undefined
  return (
    <div className={styles.field}>
      <label htmlFor={id} className={styles.label}>
        {label}
      </label>
      {children(id, hintId)}
      {hint && (
        <span id={hintId} className={styles.hint}>
          {hint}
        </span>
      )}
    </div>
  )
}

export function TextField({
  label,
  hint,
  ...rest
}: { label: string; hint?: ReactNode } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <FieldShell label={label} hint={hint}>
      {(id, hintId) => <input id={id} aria-describedby={hintId} className={styles.input} {...rest} />}
    </FieldShell>
  )
}

export function TextArea({
  label,
  hint,
  ...rest
}: { label: string; hint?: ReactNode } & TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <FieldShell label={label} hint={hint}>
      {(id, hintId) => <textarea id={id} aria-describedby={hintId} className={styles.input} {...rest} />}
    </FieldShell>
  )
}

export function SelectField({
  label,
  hint,
  children,
  ...rest
}: { label: string; hint?: ReactNode } & SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <FieldShell label={label} hint={hint}>
      {(id, hintId) => (
        <select id={id} aria-describedby={hintId} className={styles.input} {...rest}>
          {children}
        </select>
      )}
    </FieldShell>
  )
}

interface SwitchProps {
  label: string
  description?: string
  checked: boolean
  onChange: (checked: boolean) => void
}

export function Switch({ label, description, checked, onChange }: SwitchProps) {
  return (
    <label className={styles.switch}>
      <span className={styles.switchText}>
        <span className={styles.label}>{label}</span>
        {description && <span className={styles.hint}>{description}</span>}
      </span>
      <input
        type="checkbox"
        role="switch"
        aria-checked={checked}
        className="visually-hidden"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
      />
      <span className={styles.track} aria-hidden="true" />
    </label>
  )
}

type Tone = 'neutral' | 'ok' | 'warn' | 'err' | 'mono'

export function Chip({ tone = 'neutral', children }: { tone?: Tone; children: ReactNode }) {
  const toneClass = { neutral: '', ok: styles.ok, warn: styles.warn, err: styles.err, mono: styles.chipMono }[
    tone
  ]
  return <span className={[styles.chip, toneClass].join(' ')}>{children}</span>
}

export function PageHeader({
  title,
  subtitle,
  actions,
}: {
  title: string
  subtitle?: ReactNode
  actions?: ReactNode
}) {
  return (
    <header className={styles.pageHeader}>
      <div className={styles.pageTitle}>
        <h1>{title}</h1>
        {subtitle && <p className={styles.subtitle}>{subtitle}</p>}
      </div>
      {actions}
    </header>
  )
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <div className={styles.empty}>{children}</div>
}

export function ErrorNotice({ error }: { error: unknown }) {
  if (!error) return null
  const message = error instanceof Error ? error.message : 'Something went wrong.'
  return (
    <p role="alert" className={styles.notice}>
      {message}
    </p>
  )
}

export function Spinner({ label = 'Loading' }: { label?: string }) {
  return <output aria-label={label} className={styles.spinner} />
}

interface ChoiceProps<T extends string> {
  label: string
  options: readonly T[]
  value: T
  onChange: (value: T) => void
  render: (option: T) => ReactNode
}

/** A group of pressable tiles where exactly one is selected. */
export function ChoiceGroup<T extends string>({ label, options, value, onChange, render }: ChoiceProps<T>) {
  return (
    <fieldset style={{ border: 0, padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 10 }}>
      <legend className={styles.label} style={{ marginBottom: 10 }}>
        {label}
      </legend>
      <div className={styles.choices}>
        {options.map((option) => (
          <button
            key={option}
            type="button"
            className={styles.choice}
            aria-pressed={option === value}
            aria-label={`${label}: ${option}`}
            onClick={() => onChange(option)}
          >
            {render(option)}
          </button>
        ))}
      </div>
    </fieldset>
  )
}
