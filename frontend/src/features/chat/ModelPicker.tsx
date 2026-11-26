import { useState } from 'react'
import { Link } from 'react-router'
import { useModels } from '@/api/queries'
import type { ModelRef, Provider } from '@/api/types'
import { Icon } from '@/components/ui/icons'
import { usePopover } from '@/lib/usePopover'
import styles from './chat.module.css'

interface ModelPickerProps {
  providers: Provider[]
  value: ModelRef | undefined
  /** The model that actually answered last, when known. Shown for "Auto". */
  answeredWith?: string | null
  onChange: (value: ModelRef | null) => void
  disabled?: boolean
}

function ProviderModels({
  provider,
  filter,
  value,
  pick,
}: {
  provider: Provider
  filter: string
  value: ModelRef | undefined
  pick: (ref: ModelRef) => void
}) {
  const models = useModels(provider.id)
  const ids = models.data?.map((m) => m.id) ?? []
  if (provider.defaultModel && !ids.includes(provider.defaultModel)) ids.unshift(provider.defaultModel)
  const needle = filter.trim().toLowerCase()
  const shown = needle
    ? ids.filter((id) => id.toLowerCase().includes(needle) || provider.name.toLowerCase().includes(needle))
    : ids

  if (shown.length === 0 && !models.isPending) return null

  return (
    <div className={styles.modelGroup}>
      <span className={styles.modelGroupLabel}>{provider.name}</span>
      {models.isPending && ids.length === 0 && <span className={styles.modelEmpty}>Loading models…</span>}
      {shown.slice(0, 60).map((id) => {
        const selected = value?.providerId === provider.id && value.model === id
        return (
          <button
            key={id}
            type="button"
            role="menuitemradio"
            aria-checked={selected}
            className={styles.modelOption}
            onClick={() => pick({ providerId: provider.id, model: id })}
          >
            <span className={styles.modelName}>{id}</span>
            {id === provider.defaultModel && <span className={styles.modelTag}>default</span>}
            {selected && <Icon name="check" size={16} />}
          </button>
        )
      })}
      {shown.length > 60 && (
        <span className={styles.modelEmpty}>{shown.length - 60} more. Type to narrow the list.</span>
      )}
    </div>
  )
}

/** The main control of a chat: which model answers. */
export function ModelPicker({ providers, value, answeredWith, onChange, disabled }: ModelPickerProps) {
  const [filter, setFilter] = useState('')
  const popover = usePopover()
  const enabled = providers.filter((p) => p.isEnabled)

  if (enabled.length === 0) {
    return (
      <Link to="/models" className={styles.modelTrigger}>
        <span className={styles.modelDot} data-off="true" />
        Connect a model
        <Icon name="chevron" size={16} />
      </Link>
    )
  }

  const provider = enabled.find((p) => p.id === value?.providerId)
  const label = value?.model ?? (answeredWith ? `Auto · ${answeredWith}` : 'Auto')

  const close = popover.close

  return (
    <div className={styles.popoverWrap} ref={popover.ref}>
      <button
        type="button"
        className={styles.modelTrigger}
        aria-expanded={popover.open}
        aria-haspopup="true"
        aria-label={`Model: ${label}`}
        onClick={popover.toggle}
        disabled={disabled}
      >
        <span className={styles.modelDot} />
        <span className={styles.modelLabel}>{label}</span>
        {provider && <span className={styles.modelProvider}>{provider.name}</span>}
        <Icon name="chevron" size={16} />
      </button>
      {popover.open && (
        <div className={[styles.popover, styles.alignStart, styles.below].join(' ')}>
          <div className={styles.modelMenu}>
            <label className={styles.modelSearch}>
              <span className="visually-hidden">Search models</span>
              <input
                autoFocus
                value={filter}
                placeholder="Search models"
                onChange={(e) => setFilter(e.target.value)}
              />
            </label>
            <div className={styles.modelList} role="menu" aria-label="Models">
              {!filter && (
                <button
                  type="button"
                  role="menuitemradio"
                  aria-checked={!value}
                  className={styles.modelOption}
                  onClick={() => {
                    onChange(null)
                    close()
                  }}
                >
                  <span className={styles.modelName}>
                    Auto
                    <span className={styles.modelHint}>The first connected provider picks</span>
                  </span>
                  {!value && <Icon name="check" size={16} />}
                </button>
              )}
              {enabled.map((p) => (
                <ProviderModels
                  key={p.id}
                  provider={p}
                  filter={filter}
                  value={value}
                  pick={(ref) => {
                    onChange(ref)
                    close()
                  }}
                />
              ))}
            </div>
            <Link to="/models" className={styles.modelManage}>
              <Icon name="plus" size={16} /> Connect another provider
            </Link>
          </div>
        </div>
      )}
    </div>
  )
}
