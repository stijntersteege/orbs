import { useState, type FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { integrationsApi } from '@/api/endpoints'
import { useIntegrationKinds, useIntegrations } from '@/api/queries'
import type { Integration, IntegrationKind } from '@/api/types'
import { Button, Card, Chip, ErrorNotice, SelectField, TextField } from '@/components/ui'
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
        {kinds.data && kinds.data.length > 0 && <ConnectApp kinds={kinds.data} onAdded={onChange} />}
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
  const test = useMutation({ mutationFn: () => integrationsApi.test(integration.id) })
  const remove = useMutation({
    mutationFn: () => integrationsApi.remove(integration.id),
    onSuccess: onChange,
  })
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
      <Button small variant="ghost" aria-label={`Remove ${integration.name}`} onClick={() => remove.mutate()}>
        <Icon name="trash" />
      </Button>
    </div>
  )
}

function ConnectApp({ kinds, onAdded }: { kinds: IntegrationKind[]; onAdded: () => void }) {
  const [kindId, setKindId] = useState(kinds[0]!.kind)
  const [name, setName] = useState('')
  const [values, setValues] = useState<Record<string, string>>({})
  const kind = kinds.find((k) => k.kind === kindId) ?? kinds[0]!
  const secretField = kind.fields.find((f) => f.secret)

  const add = useMutation({
    mutationFn: () =>
      integrationsApi.create({
        kind: kind.kind,
        name: name || kind.displayName,
        settings: Object.fromEntries(
          kind.fields.filter((f) => !f.secret && values[f.key]).map((f) => [f.key, values[f.key]!]),
        ),
        secret: secretField ? values[secretField.key] || undefined : undefined,
      }),
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
    <form className={shared.stack} onSubmit={submit} aria-label="Connect an app">
      <div className={shared.grid2}>
        <SelectField
          label="App"
          value={kind.kind}
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
            required={field.required}
            placeholder={field.placeholder}
            hint={field.hint ?? (field.secret ? 'Stored encrypted and never shown again.' : undefined)}
            value={values[field.key] ?? ''}
            onChange={(e) => setValues((v) => ({ ...v, [field.key]: e.target.value }))}
          />
        ),
      )}
      <ErrorNotice error={add.error} />
      <div className={shared.actions}>
        <Button type="submit" variant="primary" disabled={add.isPending}>
          Connect {kind.displayName}
        </Button>
      </div>
    </form>
  )
}
