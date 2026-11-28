import { useState, type FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { integrationsApi } from '@/api/endpoints'
import { useIntegrationKinds, useIntegrations } from '@/api/queries'
import type { Integration, IntegrationKind } from '@/api/types'
import { Button, Card, Chip, ErrorNotice, Reveal, SelectField, TextField } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import shared from '../shared.module.css'

/** Apps that bring their own tools: web search, calendar, email, Home Assistant. */
export function IntegrationsCard({ onChange }: { onChange: () => void }) {
  const kinds = useIntegrationKinds()
  const integrations = useIntegrations()
  return (
    <Card>
      <div className={shared.stack}>
        <h2>Connected apps</h2>
        <p className={shared.muted}>Connect an app once; its tools are there for every orb.</p>
        <ErrorNotice error={integrations.error} />
        {integrations.data?.map((i) => (
          <IntegrationRow
            key={i.id}
            integration={i}
            kind={kinds.data?.find((k) => k.kind === i.kind)}
            onChange={onChange}
          />
        ))}
        {integrations.data?.length === 0 && <p className={shared.muted}>No apps connected yet.</p>}
        {kinds.data && kinds.data.length > 0 && (
          <Reveal label="Connect an app">
            {(close) => (
              <ConnectApp
                kinds={kinds.data}
                onAdded={() => {
                  close()
                  onChange()
                }}
              />
            )}
          </Reveal>
        )}
      </div>
    </Card>
  )
}

function IntegrationRow({
  integration,
  kind,
  onChange,
}: {
  integration: Integration
  kind?: IntegrationKind
  onChange: () => void
}) {
  const [editing, setEditing] = useState(false)
  const test = useMutation({ mutationFn: () => integrationsApi.test(integration.id) })
  const remove = useMutation({
    mutationFn: () => integrationsApi.remove(integration.id),
    onSuccess: onChange,
  })
  if (editing && kind)
    return (
      <div className={shared.editBox}>
        <ConnectApp
          kinds={[kind]}
          existing={integration}
          onAdded={() => {
            setEditing(false)
            onChange()
          }}
        />
        <div>
          <Button small variant="ghost" onClick={() => setEditing(false)}>
            Cancel
          </Button>
        </div>
      </div>
    )
  return (
    <div className={shared.row}>
      <span className={shared.rowText}>
        <strong>
          {integration.name} <Chip>{kind?.displayName ?? integration.kind}</Chip>
        </strong>
        <span className={`${shared.muted} mono`}>{integration.tools.join(', ')}</span>
        {test.data && (
          <span className={test.data.ok ? shared.muted : shared.error}>
            {test.data.ok ? 'Works.' : test.data.error}
          </span>
        )}
      </span>
      <Button small onClick={() => test.mutate()} disabled={test.isPending}>
        Test
      </Button>
      {kind && (
        <Button small onClick={() => setEditing(true)}>
          Edit
        </Button>
      )}
      <Button small variant="ghost" aria-label={`Remove ${integration.name}`} onClick={() => remove.mutate()}>
        <Icon name="trash" />
      </Button>
    </div>
  )
}

/** Connects an app, or changes one that is connected (`existing`). A blank secret keeps the saved one. */
function ConnectApp({
  kinds,
  existing,
  onAdded,
}: {
  kinds: IntegrationKind[]
  existing?: Integration
  onAdded: () => void
}) {
  const [kindId, setKindId] = useState(existing?.kind ?? kinds[0]!.kind)
  const [name, setName] = useState(existing?.name ?? '')
  const [values, setValues] = useState<Record<string, string>>(existing?.settings ?? {})
  const kind = kinds.find((k) => k.kind === kindId) ?? kinds[0]!
  const secretField = kind.fields.find((f) => f.secret)

  const add = useMutation({
    mutationFn: () => {
      const body = {
        kind: kind.kind,
        name: name || kind.displayName,
        settings: Object.fromEntries(
          kind.fields.filter((f) => !f.secret && values[f.key]).map((f) => [f.key, values[f.key]!]),
        ),
        secret: secretField ? values[secretField.key] || undefined : undefined,
        isEnabled: existing?.isEnabled ?? true,
      }
      return existing ? integrationsApi.update(existing.id, body) : integrationsApi.create(body)
    },
    onSuccess: () => {
      setName('')
      setValues({})
      onAdded()
    },
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    add.mutate()
  }

  return (
    <form
      className={shared.stack}
      onSubmit={submit}
      aria-label={existing ? `Edit ${existing.name}` : 'Connect an app'}
    >
      <div className={shared.grid2}>
        <SelectField
          label="App"
          value={kind.kind}
          disabled={!!existing}
          onChange={(e) => {
            setKindId(e.target.value)
            setValues({})
          }}
        >
          {kinds.map((k) => (
            <option key={k.kind} value={k.kind}>
              {k.displayName}
            </option>
          ))}
        </SelectField>
        <TextField
          label="Name"
          value={name}
          placeholder={kind.displayName}
          onChange={(e) => setName(e.target.value)}
        />
      </div>
      <p className={shared.muted}>{kind.description}</p>
      {kind.fields.map((field) =>
        field.options ? (
          <SelectField
            key={field.key}
            label={field.label}
            value={values[field.key] ?? field.options[0]}
            hint={field.hint}
            onChange={(e) => setValues((v) => ({ ...v, [field.key]: e.target.value }))}
          >
            {field.options.map((o) => (
              <option key={o} value={o}>
                {o}
              </option>
            ))}
          </SelectField>
        ) : (
          <TextField
            key={field.key}
            label={field.label}
            type={field.secret ? 'password' : 'text'}
            autoComplete="off"
            required={field.required && !(existing && field.secret)}
            placeholder={field.placeholder}
            hint={
              field.secret && existing?.hasSecret
                ? 'Saved. Leave empty to keep it, or type a new one.'
                : (field.hint ?? (field.secret ? 'Stored encrypted and never shown again.' : undefined))
            }
            value={values[field.key] ?? ''}
            onChange={(e) => setValues((v) => ({ ...v, [field.key]: e.target.value }))}
          />
        ),
      )}
      <ErrorNotice error={add.error} />
      <div className={shared.actions}>
        <Button type="submit" variant="primary" disabled={add.isPending}>
          {existing ? 'Save changes' : `Connect ${kind.displayName}`}
        </Button>
      </div>
    </form>
  )
}
