import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useBlocker, useNavigate, useParams } from 'react-router'
import { useMutation } from '@tanstack/react-query'
import { orbsApi } from '@/api/endpoints'
import { useContextSources, useDeleteOrb, useOrb, useProviders, useSaveOrb } from '@/api/queries'
import { DEFAULT_APPEARANCE, type Orb as OrbModel, type SaveOrbRequest } from '@/api/types'
import { Orb } from '@/components/orb/Orb'
import { saveText } from '@/lib/download'
import { plural } from '@/lib/format'
import { useMediaQuery } from '@/lib/useMediaQuery'
import {
  Button,
  ButtonLink,
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
import { OrbMaker } from './OrbMaker'
import { ToolsCard } from './ToolsCard'
import { VoicePicker } from './VoicePicker'
import styles from './orbs.module.css'

const AUTO = ''

function toForm(orb?: OrbModel): SaveOrbRequest {
  return {
    name: orb?.name ?? '',
    description: orb?.description ?? '',
    instructions: orb?.instructions ?? '',
    appearance: orb?.appearance ?? DEFAULT_APPEARANCE,
    model: orb?.model ?? null,
    contextSourceIds: orb?.contextSourceIds ?? [],
    toolPermissions: orb?.toolPermissions ?? {},
    voice: orb?.voice ?? '',
  }
}

const encodeModel = (m: SaveOrbRequest['model']) => (m ? `${m.providerId}|${m.model}` : AUTO)

function decodeModel(value: string): SaveOrbRequest['model'] {
  if (value === AUTO) return null
  const [providerId, ...rest] = value.split('|')
  return { providerId: providerId!, model: rest.join('|') }
}

/** Creates or sets up an orb: what it knows, its brain, how it behaves and its face. */
export function OrbEditorPage() {
  const { orbId } = useParams()
  const existing = useOrb(orbId)

  if (orbId && existing.isPending) return <Spinner />
  if (orbId && existing.error) return <ErrorNotice error={existing.error} />
  // Keyed so switching orbs starts a fresh form from that orb's saved values.
  return <OrbForm key={orbId ?? 'new'} orb={orbId ? existing.data : undefined} />
}

function OrbForm({ orb }: { orb?: OrbModel }) {
  const orbId = orb?.id
  const isNew = !orbId
  const navigate = useNavigate()
  const providers = useProviders()
  const sources = useContextSources()
  const save = useSaveOrb()
  const remove = useDeleteOrb()
  const [saved] = useState<SaveOrbRequest>(() => toForm(orb))
  const [form, setForm] = useState<SaveOrbRequest>(saved)
  const narrow = useMediaQuery('(max-width: 1099px)')

  // Leaving with unsaved changes asks first; saving itself moves on without asking.
  const dirty = JSON.stringify(form) !== JSON.stringify(saved)
  const leaving = useRef(false)
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      dirty && !leaving.current && currentLocation.pathname !== nextLocation.pathname,
  )
  useEffect(() => {
    if (blocker.state !== 'blocked') return
    if (window.confirm('Leave without saving? Your changes will be lost.')) blocker.proceed()
    else blocker.reset()
  }, [blocker])
  useEffect(() => {
    if (!dirty) return
    const warn = (e: BeforeUnloadEvent) => e.preventDefault()
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [dirty])

  const appearance = form.appearance ?? DEFAULT_APPEARANCE
  const selectedSources = new Set(form.contextSourceIds)
  const set = (patch: Partial<SaveOrbRequest>) => setForm((f) => ({ ...f, ...patch }))

  const toggleSource = (id: string) => {
    const next = new Set(selectedSources)
    if (next.has(id)) next.delete(id)
    else next.add(id)
    set({ contextSourceIds: [...next] })
  }

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    const result = await save.mutateAsync({ id: orbId, body: form })
    leaving.current = true
    navigate(`/orbs/${result.id}/chat`)
  }

  const onDelete = async () => {
    if (!orbId || !window.confirm(`Delete ${form.name} and all its chats? This cannot be undone.`)) return
    await remove.mutateAsync(orbId)
    leaving.current = true
    navigate('/orbs')
  }

  const typed = form.name.trim()
  // "Your orb" starts a sentence; "your orb" sits inside one.
  const name = typed || 'Your orb'
  const inline = typed || 'your orb'

  return (
    <form onSubmit={onSubmit} aria-label={isNew ? 'New orb' : `Set up ${name}`}>
      <PageHeader
        title={isNew ? 'New orb' : name}
        subtitle={isNew ? 'Pick what it knows, a brain and a face.' : 'Changes apply after you save.'}
        actions={
          !isNew && (
            <ButtonLink to={`/orbs/${orbId}/chat`}>
              <Icon name="chat" size={18} /> Open chat
            </ButtonLink>
          )
        }
      />

      <div className={styles.editor}>
        <div className={styles.sections}>
          <Card>
            <div className={styles.sectionHead}>
              <span className={styles.step}>1</span>
              <h2>Who is it</h2>
            </div>
            <div className={styles.stack}>
              <TextField
                label="Name"
                value={form.name}
                maxLength={40}
                required
                onChange={(e) => set({ name: e.target.value })}
                hint="Short and sweet works best for voice."
              />
              <TextField
                label="Description"
                value={form.description}
                maxLength={280}
                placeholder="Work sidekick"
                onChange={(e) => set({ description: e.target.value })}
              />
            </div>
          </Card>

          <Card>
            <div className={styles.sectionHead}>
              <span className={styles.step}>2</span>
              <h2>What {inline} knows</h2>
            </div>
            <p className={styles.muted} style={{ marginBottom: 16 }}>
              {name} only sees what you switch on here.
            </p>
            {sources.data?.length === 0 && (
              <p className={styles.muted}>
                No context yet. <Link to="/context">Add a note or a web page</Link> first.
              </p>
            )}
            <ul className={styles.checkList}>
              {sources.data?.map((source) => (
                <li key={source.id}>
                  <label className={styles.check}>
                    <input
                      type="checkbox"
                      checked={selectedSources.has(source.id)}
                      onChange={() => toggleSource(source.id)}
                    />
                    <span className={styles.checkText}>
                      <strong>{source.name}</strong>
                      <span className={styles.muted}>
                        {source.location || `${source.contentLength.toLocaleString()} characters`}
                      </span>
                    </span>
                    <Chip
                      tone={source.status === 'ready' ? 'ok' : source.status === 'failed' ? 'err' : 'warn'}
                    >
                      {source.status}
                    </Chip>
                  </label>
                </li>
              ))}
            </ul>
          </Card>

          <Card>
            <div className={styles.sectionHead}>
              <span className={styles.step}>3</span>
              <h2>{name}&apos;s brain</h2>
            </div>
            <SelectField
              label="Model"
              value={encodeModel(form.model)}
              onChange={(e) => set({ model: decodeModel(e.target.value) })}
              hint="Auto uses the default model of your first enabled provider."
            >
              <option value={AUTO}>Auto · best available</option>
              {providers.data
                ?.filter((p) => p.defaultModel)
                .map((p) => (
                  <option key={p.id} value={`${p.id}|${p.defaultModel}`}>
                    {p.name} · {p.defaultModel}
                  </option>
                ))}
              {form.model &&
                !providers.data?.some((p) => encodeModel(form.model) === `${p.id}|${p.defaultModel}`) && (
                  <option value={encodeModel(form.model)}>{form.model.model}</option>
                )}
            </SelectField>
          </Card>

          <ToolsCard
            name={inline}
            step={4}
            permissions={form.toolPermissions ?? {}}
            onChange={(toolPermissions) => set({ toolPermissions })}
          />

          <Card>
            <div className={styles.sectionHead}>
              <span className={styles.step}>5</span>
              <h2>How {inline} behaves</h2>
            </div>
            <TextArea
              label="Brief"
              value={form.instructions}
              maxLength={8000}
              rows={5}
              placeholder="You help me run product. Lead with the answer, keep it short, and say which doc you used."
              hint="Write it like you would brief a new teammate."
              onChange={(e) => set({ instructions: e.target.value })}
            />
          </Card>

          <Card>
            <div className={styles.sectionHead}>
              <span className={styles.step}>6</span>
              <h2>Give it a face and a voice</h2>
            </div>
            <OrbMaker value={appearance} onChange={(a) => set({ appearance: a })} />
            <div style={{ marginTop: 24 }}>
              <VoicePicker name={inline} value={form.voice ?? ''} onChange={(voice) => set({ voice })} />
            </div>
          </Card>

          {!isNew && orbId && <ShareCard orbId={orbId} name={name} />}

          {!isNew && (
            <Card>
              <h2 style={{ marginBottom: 12 }}>Danger zone</h2>
              <Button variant="danger" onClick={onDelete} disabled={remove.isPending}>
                <Icon name="trash" size={18} /> Delete {name}
              </Button>
            </Card>
          )}
        </div>

        <aside className={styles.preview} aria-label="Preview">
          <Orb appearance={appearance} size={narrow ? 88 : 200} label={`${name}, preview`} />
          <div className={styles.previewText}>
            <span className={styles.previewName}>{name}</span>
            {form.description && <span className={styles.muted}>{form.description}</span>}
            <div className={styles.toggles}>
              <Chip tone="mono">{form.model?.model ?? 'auto'}</Chip>
              <Chip tone="ok">{plural(selectedSources.size, 'source')} linked</Chip>
            </div>
          </div>
        </aside>
      </div>

      {/* Always in reach, however far down the form you are. */}
      <div className={styles.saveBar}>
        <ErrorNotice error={save.error ?? remove.error} />
        <span className={styles.saveStatus} aria-live="polite">
          {!typed ? 'Give it a name to continue.' : dirty ? 'Unsaved changes' : isNew ? '' : 'No changes'}
        </span>
        {!isNew && dirty && (
          <Button variant="ghost" onClick={() => setForm(saved)}>
            Undo changes
          </Button>
        )}
        <Button type="submit" variant="primary" disabled={save.isPending || !typed || (!isNew && !dirty)}>
          {save.isPending ? 'Saving…' : isNew ? (typed ? `Create ${typed}` : 'Create orb') : 'Save changes'}
        </Button>
      </div>
    </form>
  )
}

/** Exports the saved orb as a template file others can import. */
function ShareCard({ orbId, name }: { orbId: string; name: string }) {
  const exportTemplate = useMutation({
    mutationFn: () => orbsApi.template(orbId),
    onSuccess: (template) =>
      saveText(`${name || 'orb'}.orb.json`, JSON.stringify(template, null, 2) + '\n', 'application/json'),
  })
  return (
    <Card>
      <h2 style={{ marginBottom: 8 }}>Share</h2>
      <p className={styles.muted} style={{ marginBottom: 12 }}>
        Save {name} as a template file: its face, brief, voice, tool settings and schedules. Anyone can import
        it under Your orbs. The model, what it can read, chats and memories stay here.
      </p>
      <ErrorNotice error={exportTemplate.error} />
      <Button onClick={() => exportTemplate.mutate()} disabled={exportTemplate.isPending}>
        Export as template
      </Button>
    </Card>
  )
}
