import { useState, type FormEvent } from 'react'
import {
  useContextKinds,
  useContextSources,
  useDeleteContextSource,
  useOrbs,
  useSaveContextSource,
  useSyncContextSource,
  useUploadContextFile,
} from '@/api/queries'
import type { ContextKind, ContextSource } from '@/api/types'
import {
  Button,
  Card,
  Chip,
  EmptyState,
  ErrorNotice,
  PageHeader,
  Spinner,
  TextArea,
  TextField,
} from '@/components/ui'
import { Icon, type IconName } from '@/components/ui/icons'
import styles from '../shared.module.css'

const KIND_ICON: Record<string, IconName> = { text: 'note', url: 'globe', file: 'file' }

function statusChip(source: ContextSource) {
  if (source.status === 'ready') return <Chip tone="ok">● Synced</Chip>
  if (source.status === 'failed') return <Chip tone="err">Failed</Chip>
  return <Chip tone="warn">◐ Pending</Chip>
}

function AddSourceForm({ kinds, onDone }: { kinds: ContextKind[]; onDone: () => void }) {
  const [kind, setKind] = useState(kinds[0]?.kind ?? 'text')
  const [name, setName] = useState('')
  const [location, setLocation] = useState('')
  const [input, setInput] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const save = useSaveContextSource()
  const upload = useUploadContextFile()
  const selected = kinds.find((k) => k.kind === kind)
  const isFile = kind === 'file'

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (isFile) {
      if (!file) return
      await upload.mutateAsync({ file, name: name || undefined })
    } else {
      await save.mutateAsync({
        body: { name, kind, location: location || undefined, input: input || undefined },
      })
    }
    onDone()
  }

  return (
    <Card>
      <form className={styles.stack} onSubmit={submit} aria-label="Add a source">
        <h2>Add a source</h2>
        <fieldset className={styles.kindGrid}>
          <legend className="visually-hidden">Kind of source</legend>
          {kinds.map((k) => (
            <label key={k.kind} className={styles.kind}>
              <input
                type="radio"
                name="kind"
                className="visually-hidden"
                checked={k.kind === kind}
                onChange={() => setKind(k.kind)}
              />
              <Icon name={KIND_ICON[k.kind] ?? 'plug'} />
              <strong>{k.displayName}</strong>
              <span className={styles.muted}>{k.description}</span>
            </label>
          ))}
        </fieldset>
        {isFile && (
          <label className={styles.fileDrop}>
            <span>{file ? file.name : 'Choose a PDF, Word or Markdown document, or a picture'}</span>
            <input
              type="file"
              aria-label="File"
              accept=".pdf,.docx,.md,.markdown,.txt,.csv,.json,.html,.png,.jpg,.jpeg,.gif,.webp"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            />
          </label>
        )}
        <TextField
          label="Name"
          value={name}
          required={!isFile}
          hint={isFile ? 'Optional. Defaults to the file name.' : undefined}
          maxLength={80}
          onChange={(e) => setName(e.target.value)}
          placeholder="Team handbook"
        />
        {selected?.usesLocation && (
          <TextField
            label="Link"
            type="url"
            required
            value={location}
            onChange={(e) => setLocation(e.target.value)}
            placeholder="https://example.com/docs"
          />
        )}
        {selected?.usesInput && (
          <TextArea
            label="Content"
            required
            rows={6}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="Anything your orbs should know."
          />
        )}
        <ErrorNotice error={save.error ?? upload.error} />
        <div className={styles.actions}>
          <Button
            type="submit"
            variant="primary"
            disabled={save.isPending || upload.isPending || (isFile && !file)}
          >
            {save.isPending || upload.isPending ? 'Connecting…' : isFile ? 'Upload' : 'Connect'}
          </Button>
          <Button variant="ghost" onClick={onDone}>
            Cancel
          </Button>
        </div>
      </form>
    </Card>
  )
}

/** Everything your orbs can know. Connect a source once, then choose which orbs may use it. */
export function ContextPage() {
  const sources = useContextSources()
  const kinds = useContextKinds()
  const orbs = useOrbs()
  const sync = useSyncContextSource()
  const remove = useDeleteContextSource()
  const [adding, setAdding] = useState(false)

  const usedBy = (id: string) =>
    orbs.data?.filter((o) => o.contextSourceIds.includes(id)).map((o) => o.name) ?? []

  return (
    <>
      <PageHeader
        title="Context"
        subtitle="Everything your orbs can know. Connect a source once, then choose which orbs may use it."
        actions={
          !adding && (
            <Button variant="primary" onClick={() => setAdding(true)}>
              <Icon name="plus" size={18} /> Add a source
            </Button>
          )
        }
      />
      <div className={styles.stack}>
        {adding && kinds.data && <AddSourceForm kinds={kinds.data} onDone={() => setAdding(false)} />}
        <ErrorNotice error={sources.error ?? sync.error ?? remove.error} />
        {sources.isPending && <Spinner />}
        {sources.data?.length === 0 && !adding && (
          <EmptyState>
            <Icon name="context" size={32} />
            <p>No context yet. Add a note or a web page and your orbs can use it in answers.</p>
          </EmptyState>
        )}
        {sources.data && sources.data.length > 0 && (
          <ul className={styles.list} aria-label="Connected sources">
            {sources.data.map((s) => {
              const users = usedBy(s.id)
              return (
                <li key={s.id} className={styles.row}>
                  <span className={styles.rowIcon}>
                    <Icon name={KIND_ICON[s.kind] ?? 'plug'} />
                  </span>
                  <span className={styles.rowText}>
                    <strong>{s.name}</strong>
                    <span className={styles.muted}>
                      {s.location || `${s.contentLength.toLocaleString()} characters`}
                      {' · '}
                      {users.length > 0 ? `Used by ${users.join(', ')}` : 'Not used by any orb yet'}
                    </span>
                    {s.lastError && <span className={styles.error}>{s.lastError}</span>}
                  </span>
                  {statusChip(s)}
                  {(s.indexedChunks ?? 0) > 0 && (
                    <Chip tone="mono">
                      {s.indexedChunks} {s.indexedChunks === 1 ? 'passage' : 'passages'}
                    </Chip>
                  )}
                  <Button
                    small
                    variant="ghost"
                    iconOnly
                    aria-label={`Sync ${s.name}`}
                    onClick={() => sync.mutate(s.id)}
                  >
                    <Icon name="refresh" size={18} />
                  </Button>
                  <Button
                    small
                    variant="ghost"
                    iconOnly
                    aria-label={`Delete ${s.name}`}
                    onClick={() =>
                      window.confirm(`Delete ${s.name}? Orbs that use it will no longer see it.`) &&
                      remove.mutate(s.id)
                    }
                  >
                    <Icon name="trash" size={18} />
                  </Button>
                </li>
              )
            })}
          </ul>
        )}
      </div>
    </>
  )
}
