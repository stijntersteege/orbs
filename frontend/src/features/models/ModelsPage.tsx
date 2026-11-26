import { useState, type FormEvent } from 'react'
import { useDeleteProvider, useModels, useProviderKinds, useProviders, useSaveProvider } from '@/api/queries'
import type { Provider } from '@/api/types'
import {
  Button,
  Card,
  Chip,
  EmptyState,
  ErrorNotice,
  PageHeader,
  SelectField,
  Spinner,
  Switch,
  TextField,
} from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import styles from '../shared.module.css'
import { PROVIDER_PRESETS, type ProviderPreset } from './presets'

interface ProviderFormProps {
  provider?: Provider
  preset?: ProviderPreset
  onDone: () => void
}

function ProviderForm({ provider, preset, onDone }: ProviderFormProps) {
  const kinds = useProviderKinds()
  const save = useSaveProvider()
  const models = useModels(provider?.id)
  const [name, setName] = useState(provider?.name ?? preset?.name ?? '')
  const [kind] = useState(provider?.kind ?? preset?.kind ?? 'openai-compatible')
  const [baseUrl, setBaseUrl] = useState(provider?.baseUrl ?? preset?.baseUrl ?? '')
  const [apiKey, setApiKey] = useState('')
  const [defaultModel, setDefaultModel] = useState(provider?.defaultModel ?? preset?.defaultModel ?? '')
  const [isEnabled, setIsEnabled] = useState(provider?.isEnabled ?? true)
  const kindInfo = kinds.data?.find((k) => k.kind === kind)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    await save.mutateAsync({
      id: provider?.id,
      body: {
        name,
        kind,
        baseUrl: baseUrl || undefined,
        // Leaving the key empty while editing keeps the stored key.
        apiKey: apiKey || undefined,
        defaultModel: defaultModel || undefined,
        isEnabled,
      },
    })
    onDone()
  }

  return (
    <Card>
      <form
        className={styles.stack}
        onSubmit={submit}
        aria-label={provider ? `Edit ${provider.name}` : 'Connect a provider'}
      >
        <div>
          <h2>{provider ? provider.name : `Connect ${preset?.name ?? 'a provider'}`}</h2>
          <p className={styles.muted}>{kindInfo?.description ?? preset?.hint}</p>
        </div>
        <div className={styles.grid2}>
          <TextField
            label="Name"
            required
            value={name}
            maxLength={80}
            onChange={(e) => setName(e.target.value)}
          />
          {kindInfo?.requiresBaseUrl !== false && (
            <TextField
              label="Base URL"
              type="url"
              required={kindInfo?.requiresBaseUrl}
              value={baseUrl}
              placeholder="https://api.example.com/v1"
              onChange={(e) => setBaseUrl(e.target.value)}
            />
          )}
        </div>
        {kind !== 'echo' && (
          <TextField
            label="API key"
            type="password"
            autoComplete="off"
            value={apiKey}
            required={!provider?.hasApiKey && (kindInfo?.requiresApiKey ?? false)}
            placeholder={
              provider?.hasApiKey
                ? '•••••••• stored, leave empty to keep'
                : preset?.needsKey
                  ? 'Paste your key'
                  : 'Optional'
            }
            onChange={(e) => setApiKey(e.target.value)}
            hint="Keys are encrypted on your server and only used to call this provider."
          />
        )}
        {provider && models.data && models.data.length > 0 ? (
          <SelectField
            label="Default model"
            value={defaultModel}
            onChange={(e) => setDefaultModel(e.target.value)}
          >
            <option value="">None</option>
            {models.data.map((m) => (
              <option key={m.id} value={m.id}>
                {m.displayName ?? m.id}
              </option>
            ))}
          </SelectField>
        ) : (
          <TextField
            label="Default model"
            value={defaultModel}
            placeholder="for example llama3.2 or gpt-4o-mini"
            onChange={(e) => setDefaultModel(e.target.value)}
            hint={
              models.error ? `Could not list models: ${models.error.message}` : 'Orbs on Auto use this model.'
            }
          />
        )}
        <Switch
          label="Enabled"
          description="Disabled providers are never called."
          checked={isEnabled}
          onChange={setIsEnabled}
        />
        <ErrorNotice error={save.error} />
        <div className={styles.actions}>
          <Button type="submit" variant="primary" disabled={save.isPending}>
            {provider ? 'Save' : 'Connect'}
          </Button>
          <Button variant="ghost" onClick={onDone}>
            Cancel
          </Button>
        </div>
      </form>
    </Card>
  )
}

/** Connect any provider. Every orb can use anything connected here. */
export function ModelsPage() {
  const providers = useProviders()
  const remove = useDeleteProvider()
  const [editing, setEditing] = useState<Provider | null>(null)
  const [preset, setPreset] = useState<ProviderPreset | null>(null)
  const [choosing, setChoosing] = useState(false)
  const close = () => {
    setEditing(null)
    setPreset(null)
    setChoosing(false)
  }

  return (
    <>
      <PageHeader
        title="Models"
        subtitle="Connect any provider. Every orb can use anything you connect here."
        actions={
          !choosing &&
          !preset &&
          !editing && (
            <Button variant="primary" onClick={() => setChoosing(true)}>
              <Icon name="plus" size={18} /> Add provider
            </Button>
          )
        }
      />
      <div className={styles.stack}>
        {choosing && !preset && (
          <Card>
            <div className={styles.stack}>
              <h2>Pick a way to start</h2>
              <div className={styles.kindGrid}>
                {PROVIDER_PRESETS.map((p) => (
                  <button key={p.id} type="button" className={styles.kind} onClick={() => setPreset(p)}>
                    <strong>{p.name}</strong>
                    <span className={styles.muted}>{p.hint}</span>
                  </button>
                ))}
              </div>
              <div>
                <Button variant="ghost" onClick={close}>
                  Cancel
                </Button>
              </div>
            </div>
          </Card>
        )}
        {preset && <ProviderForm preset={preset} onDone={close} />}
        {editing && <ProviderForm key={editing.id} provider={editing} onDone={close} />}

        <ErrorNotice error={providers.error ?? remove.error} />
        {providers.isPending && <Spinner />}
        {providers.data?.length === 0 && !choosing && !preset && (
          <EmptyState>
            <Icon name="models" size={32} />
            <p>No providers yet. Connect one and your orbs get a brain.</p>
          </EmptyState>
        )}
        {providers.data && providers.data.length > 0 && (
          <ul className={styles.list} aria-label="Providers">
            {providers.data.map((p) => (
              <li key={p.id} className={styles.row}>
                <span className={styles.rowIcon}>{p.name.slice(0, 2).toUpperCase()}</span>
                <span className={styles.rowText}>
                  <strong>{p.name}</strong>
                  <span className={styles.muted}>
                    <span className="mono">{p.defaultModel ?? 'no default model'}</span>
                    {p.baseUrl ? ` · ${p.baseUrl}` : ''}
                  </span>
                </span>
                {p.isEnabled ? <Chip tone="ok">● Connected</Chip> : <Chip>Off</Chip>}
                <Button small onClick={() => setEditing(p)}>
                  Manage
                </Button>
                <Button
                  small
                  variant="ghost"
                  iconOnly
                  aria-label={`Remove ${p.name}`}
                  onClick={() =>
                    window.confirm(`Remove ${p.name}? Orbs pinned to it go back to Auto.`) &&
                    remove.mutate(p.id)
                  }
                >
                  <Icon name="trash" size={18} />
                </Button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </>
  )
}
