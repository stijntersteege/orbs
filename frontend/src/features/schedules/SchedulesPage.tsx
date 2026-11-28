import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { apiBase } from '@/api/client'
import { schedulesApi } from '@/api/endpoints'
import { keys, useIntegrations, useOrbs, useSchedules } from '@/api/queries'
import type { Schedule, TriggerKind } from '@/api/types'
import {
  Button,
  Card,
  Chip,
  ErrorNotice,
  PageHeader,
  SelectField,
  Spinner,
  TextArea,
  TextField,
} from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import shared from '../shared.module.css'

const TRIGGERS: { id: TriggerKind; label: string }[] = [
  { id: 'time', label: 'At set times' },
  { id: 'webhook', label: 'When another app calls' },
  { id: 'rss', label: 'When a feed has something new' },
  { id: 'email', label: 'When new email arrives' },
]

const PRESETS: { label: string; cron: string }[] = [
  { label: 'Every day at 8:00', cron: '0 8 * * *' },
  { label: 'Every Monday at 9:00', cron: '0 9 * * 1' },
  { label: 'Every weekday at 17:30', cron: '30 17 * * 1-5' },
  { label: 'Every hour', cron: '0 * * * *' },
]

/** The browser's IANA time zone, such as Europe/Amsterdam. */
const localTimeZone = () => new Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'

const when = (iso?: string) =>
  iso ? new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : '—'

function describe(s: Schedule): string {
  switch (s.trigger) {
    case 'time':
      return `${s.cron} (${s.timeZone}) · next ${when(s.nextCheckAt)}`
    case 'webhook':
      return 'When its webhook is called'
    case 'rss':
      return `Watches ${s.feedUrl}`
    case 'email':
      return 'Watches your inbox'
  }
}

/** Orbs that work on their own: at set times, on a webhook, or when something new arrives. */
export function SchedulesPage() {
  const schedules = useSchedules()
  const orbs = useOrbs()
  const client = useQueryClient()
  const refresh = () => client.invalidateQueries({ queryKey: keys.schedules })
  const [webhook, setWebhook] = useState<string | null>(null)

  return (
    <>
      <PageHeader
        title="Schedules"
        subtitle="Let orbs work on their own. Each schedule writes into its own chat with the orb."
      />
      <div className={shared.stack} style={{ maxWidth: 760 }}>
        {webhook && (
          <Card>
            <div className={shared.stack}>
              <h2>Your webhook address</h2>
              <p className={shared.muted}>
                Copy it now: it holds a secret and is not shown again. POST to it; the request body goes to
                the orb.
              </p>
              <code className="mono" style={{ wordBreak: 'break-all' }}>
                {webhook}
              </code>
              <div className={shared.actions}>
                <Button small onClick={() => void navigator.clipboard?.writeText(webhook)}>
                  Copy
                </Button>
                <Button small variant="ghost" onClick={() => setWebhook(null)}>
                  Done
                </Button>
              </div>
            </div>
          </Card>
        )}

        <Card>
          {schedules.isPending && <Spinner />}
          <ErrorNotice error={schedules.error} />
          {schedules.data?.length === 0 && <p className={shared.muted}>No schedules yet.</p>}
          <ul className={shared.list} aria-label="Schedules">
            {schedules.data?.map((s) => (
              <ScheduleRow
                key={s.id}
                schedule={s}
                orbs={orbs.data?.map((o) => ({ id: o.id, name: o.name })) ?? []}
                onChange={refresh}
              />
            ))}
          </ul>
        </Card>

        {orbs.data && orbs.data.length > 0 && (
          <ScheduleForm
            orbs={orbs.data.map((o) => ({ id: o.id, name: o.name }))}
            onSaved={(s) => {
              if (s.webhookPath) setWebhook(`${apiBase() || window.location.origin}${s.webhookPath}`)
              void refresh()
            }}
          />
        )}
      </div>
    </>
  )
}

/** The fields a schedule is saved with, for changing just one of them. */
const toRequest = (s: Schedule) => ({
  orbId: s.orbId,
  name: s.name,
  prompt: s.prompt,
  trigger: s.trigger,
  cron: s.cron,
  timeZone: s.timeZone,
  feedUrl: s.feedUrl,
  integrationId: s.integrationId,
  isEnabled: s.isEnabled,
})

function ScheduleRow({
  schedule,
  orbs,
  onChange,
}: {
  schedule: Schedule
  orbs: { id: string; name: string }[]
  onChange: () => void
}) {
  const [editing, setEditing] = useState(false)
  const orbName = orbs.find((o) => o.id === schedule.orbId)?.name ?? 'Orb'
  const run = useMutation({ mutationFn: () => schedulesApi.run(schedule.id) })
  const remove = useMutation({ mutationFn: () => schedulesApi.remove(schedule.id), onSuccess: onChange })
  const toggle = useMutation({
    mutationFn: () =>
      schedulesApi.update(schedule.id, { ...toRequest(schedule), isEnabled: !schedule.isEnabled }),
    onSuccess: onChange,
  })
  if (editing && orbs.length > 0)
    return (
      <li className={shared.editBox}>
        <ScheduleForm
          orbs={orbs}
          existing={schedule}
          onSaved={() => {
            setEditing(false)
            onChange()
          }}
        />
        <div>
          <Button small variant="ghost" onClick={() => setEditing(false)}>
            Cancel
          </Button>
        </div>
      </li>
    )
  return (
    <li className={shared.row}>
      <span className={shared.rowText}>
        <strong>
          {schedule.name} <Chip>{orbName}</Chip> {!schedule.isEnabled && <Chip tone="warn">off</Chip>}
        </strong>
        <span className={shared.muted}>{describe(schedule)}</span>
        <span className={shared.muted}>
          Last run {when(schedule.lastRunAt)}
          {schedule.conversationId && (
            <>
              {' · '}
              <Link to={`/orbs/${schedule.orbId}/chat/${schedule.conversationId}`}>Open chat</Link>
            </>
          )}
        </span>
        {run.isSuccess && <span className={shared.muted}>Started. It runs in the background.</span>}
      </span>
      <Button small onClick={() => run.mutate()} disabled={run.isPending}>
        Run now
      </Button>
      <Button small onClick={() => setEditing(true)}>
        Edit
      </Button>
      <Button small variant="ghost" onClick={() => toggle.mutate()} disabled={toggle.isPending}>
        {schedule.isEnabled ? 'Pause' : 'Resume'}
      </Button>
      <Button small variant="ghost" aria-label={`Delete ${schedule.name}`} onClick={() => remove.mutate()}>
        <Icon name="trash" />
      </Button>
    </li>
  )
}

/** Creates a schedule, or changes one (`existing`). A webhook keeps its address when edited. */
function ScheduleForm({
  orbs,
  existing,
  onSaved,
}: {
  orbs: { id: string; name: string }[]
  existing?: Schedule
  onSaved: (schedule: Schedule) => void
}) {
  const integrations = useIntegrations()
  const emailAccounts = integrations.data?.filter((i) => i.kind === 'email') ?? []
  const [orbId, setOrbId] = useState(existing?.orbId ?? orbs[0]!.id)
  const [name, setName] = useState(existing?.name ?? '')
  const [prompt, setPrompt] = useState(existing?.prompt ?? '')
  const [trigger, setTrigger] = useState<TriggerKind>(existing?.trigger ?? 'time')
  const [cron, setCron] = useState(existing?.cron ?? PRESETS[1]!.cron)
  const [timeZone, setTimeZone] = useState(existing?.timeZone ?? localTimeZone)
  const [feedUrl, setFeedUrl] = useState(existing?.feedUrl ?? '')
  const [integrationId, setIntegrationId] = useState(existing?.integrationId ?? '')

  const create = useMutation({
    mutationFn: () => {
      const body = {
        orbId,
        name,
        prompt,
        trigger,
        cron: trigger === 'time' ? cron : undefined,
        timeZone: trigger === 'time' ? timeZone : undefined,
        feedUrl: trigger === 'rss' ? feedUrl : undefined,
        integrationId: trigger === 'email' ? integrationId || emailAccounts[0]?.id : undefined,
        isEnabled: existing?.isEnabled ?? true,
      }
      return existing ? schedulesApi.update(existing.id, body) : schedulesApi.create(body)
    },
    onSuccess: (s) => {
      if (!existing) {
        setName('')
        setPrompt('')
      }
      onSaved(s)
    },
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    create.mutate()
  }

  const form = (
    <form
      className={shared.stack}
      onSubmit={submit}
      aria-label={existing ? `Edit ${existing.name}` : 'New schedule'}
    >
      <h2>{existing ? `Edit ${existing.name}` : 'New schedule'}</h2>
      <div className={shared.grid2}>
        <SelectField label="Orb" value={orbId} onChange={(e) => setOrbId(e.target.value)}>
          {orbs.map((o) => (
            <option key={o.id} value={o.id}>
              {o.name}
            </option>
          ))}
        </SelectField>
        <TextField
          label="Name"
          required
          value={name}
          placeholder="Weekly summary"
          onChange={(e) => setName(e.target.value)}
        />
      </div>
      <TextArea
        label="What should the orb do?"
        required
        rows={3}
        value={prompt}
        placeholder="Summarize my week and list what is still open."
        onChange={(e) => setPrompt(e.target.value)}
      />
      <SelectField label="When" value={trigger} onChange={(e) => setTrigger(e.target.value as TriggerKind)}>
        {TRIGGERS.map((t) => (
          <option key={t.id} value={t.id}>
            {t.label}
          </option>
        ))}
      </SelectField>
      {trigger === 'time' && (
        <>
          <div className={shared.actions}>
            {PRESETS.map((p) => (
              <Button
                key={p.cron}
                small
                variant={p.cron === cron ? 'primary' : 'secondary'}
                onClick={() => setCron(p.cron)}
              >
                {p.label}
              </Button>
            ))}
          </div>
          <div className={shared.grid2}>
            <TextField
              label="Cron expression"
              value={cron}
              onChange={(e) => setCron(e.target.value)}
              hint="Minute, hour, day of month, month, day of week."
            />
            <TextField label="Time zone" value={timeZone} onChange={(e) => setTimeZone(e.target.value)} />
          </div>
        </>
      )}
      {trigger === 'rss' && (
        <TextField
          label="Feed address"
          type="url"
          required
          value={feedUrl}
          placeholder="https://example.com/feed.xml"
          onChange={(e) => setFeedUrl(e.target.value)}
          hint="Checked every 15 minutes. The first check only remembers what is already there."
        />
      )}
      {trigger === 'email' &&
        (emailAccounts.length === 0 ? (
          <p className={shared.muted}>
            Connect an email account under <Link to="/tools">Tools</Link> first.
          </p>
        ) : (
          <SelectField
            label="Email account"
            value={integrationId || emailAccounts[0]!.id}
            onChange={(e) => setIntegrationId(e.target.value)}
          >
            {emailAccounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.name}
              </option>
            ))}
          </SelectField>
        ))}
      {trigger === 'webhook' && (
        <p className={shared.muted}>
          You get a secret address after saving. Anything posted to it goes to the orb.
        </p>
      )}
      <ErrorNotice error={create.error} />
      <div className={shared.actions}>
        <Button type="submit" variant="primary" disabled={create.isPending}>
          {existing ? 'Save changes' : 'Create schedule'}
        </Button>
      </div>
    </form>
  )
  // In a list row it opens in place; on its own it gets a card.
  return existing ? form : <Card>{form}</Card>
}
