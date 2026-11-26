import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useContextSources, useDeleteOrb, useOrb, useProviders, useSaveOrb } from '@/api/queries'
import { DEFAULT_APPEARANCE, type Orb as OrbModel, type SaveOrbRequest } from '@/api/types'
import { Orb } from '@/components/orb/Orb'
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
  const [form, setForm] = useState<SaveOrbRequest>(() => toForm(orb))

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
    const saved = await save.mutateAsync({ id: orbId, body: form })
    navigate(`/orbs/${saved.id}/chat`)
  }

  const onDelete = async () => {
    if (!orbId || !window.confirm(`Delete ${form.name} and all its chats? This cannot be undone.`)) return
    await remove.mutateAsync(orbId)
    navigate('/')
  }

  const name = form.name.trim() || 'Your orb'

  return (
    <form onSubmit={onSubmit} aria-label={isNew ? 'New orb' : `Set up ${name}`}>
      <PageHeader
        title={isNew ? 'New orb' : name}
        subtitle={isNew ? 'Pick what it knows, a brain and a face.' : 'Changes apply after you save.'}
        actions={
          <div className={styles.actions}>
            {!isNew && (
              <ButtonLink to={`/orbs/${orbId}/chat`}>
                <Icon name="chat" size={18} /> Open chat
              </ButtonLink>
            )}
            <Button type="submit" variant="primary" disabled={save.isPending || !form.name.trim()}>
              {isNew ? `Create ${name}` : 'Save'}
            </Button>
          </div>
        }
      />
      <ErrorNotice error={save.error ?? remove.error} />

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
              <h2>What {name} knows</h2>
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

          <Card>
            <div className={styles.sectionHead}>
              <span className={styles.step}>4</span>
              <h2>How {name} behaves</h2>
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
              <span className={styles.step}>5</span>
              <h2>Give it a face</h2>
            </div>
            <OrbMaker value={appearance} onChange={(a) => set({ appearance: a })} />
          </Card>

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
          <Orb appearance={appearance} size={200} label={`${name}, preview`} />
          <span className={styles.previewName}>{name}</span>
          {form.description && <span className={styles.muted}>{form.description}</span>}
          <div className={styles.toggles} style={{ justifyContent: 'center' }}>
            <Chip tone="mono">{form.model?.model ?? 'auto'}</Chip>
            <Chip tone="ok">{selectedSources.size} contexts linked</Chip>
          </div>
        </aside>
      </div>
    </form>
  )
}
