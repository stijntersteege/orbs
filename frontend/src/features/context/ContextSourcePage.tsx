import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { contextApi } from '@/api/endpoints'
import {
  keys,
  useContextSources,
  useDeleteContextSource,
  useOrbs,
  useSaveContextSource,
  useSyncContextSource,
} from '@/api/queries'
import type { ContextContent, ContextSource, SyncInterval } from '@/api/types'
import { Markdown } from '@/components/markdown/Markdown'
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
import styles from './context.module.css'

const KIND_LABEL: Record<string, string> = { text: 'Note', url: 'Web page', file: 'File' }

const size = (bytes?: number) =>
  bytes === undefined
    ? ''
    : bytes < 1024
      ? `${bytes} B`
      : bytes < 1024 * 1024
        ? `${(bytes / 1024).toFixed(1)} KB`
        : `${(bytes / 1024 / 1024).toFixed(1)} MB`

const isMarkdown = (content: ContextContent) =>
  content.kind === 'text' ||
  content.contentType === 'text/markdown' ||
  /\.(md|markdown)$/i.test(content.fileName ?? '')

const codeLanguage = (content: ContextContent) =>
  content.contentType === 'application/json' ? 'json' : content.contentType === 'text/csv' ? 'csv' : ''

/** One context source: what it holds, as orbs read it, and everything you can do with it. */
export function ContextSourcePage() {
  const { sourceId } = useParams()
  const sources = useContextSources()
  const source = sources.data?.find((s) => s.id === sourceId)

  if (sources.isPending) return <Spinner />
  if (sources.error) return <ErrorNotice error={sources.error} />
  if (!source)
    return (
      <>
        <PageHeader title="Source not found" subtitle="It may have been deleted." />
        <Link to="/context">Back to Context</Link>
      </>
    )
  return <SourceView key={source.id} source={source} />
}

function SourceView({ source }: { source: ContextSource }) {
  const navigate = useNavigate()
  const client = useQueryClient()
  const orbs = useOrbs()
  const sync = useSyncContextSource()
  const remove = useDeleteContextSource()
  const content = useQuery({
    queryKey: ['context', source.id, 'content', source.updatedAt],
    queryFn: () => contextApi.content(source.id),
  })
  const [editing, setEditing] = useState(false)
  const usedBy = orbs.data?.filter((o) => o.contextSourceIds.includes(source.id)).map((o) => o.name) ?? []
  const refresh = () => void client.invalidateQueries({ queryKey: keys.context })

  const download = useMutation({
    mutationFn: async () => {
      const blob = await contextApi.download(source.id)
      const link = document.createElement('a')
      link.href = URL.createObjectURL(blob)
      link.download = content.data?.fileName ?? source.name
      link.click()
      URL.revokeObjectURL(link.href)
    },
  })

  return (
    <>
      <PageHeader
        title={source.name}
        subtitle={[
          KIND_LABEL[source.kind] ?? source.kind,
          content.data?.fileName,
          size(content.data?.size),
          usedBy.length ? `Used by ${usedBy.join(', ')}` : 'Not used by any orb yet',
        ]
          .filter(Boolean)
          .join(' · ')}
        actions={
          <div className={shared.actions}>
            <Link to="/context" className={shared.muted}>
              ← All context
            </Link>
          </div>
        }
      />
      <div className={shared.stack} style={{ maxWidth: 900 }}>
        <Card>
          <div className={styles.toolbar}>
            {source.status === 'failed' ? (
              <Chip tone="err">Failed</Chip>
            ) : source.status === 'ready' ? (
              <Chip tone="ok">Ready</Chip>
            ) : (
              <Chip tone="warn">Reading…</Chip>
            )}
            {(source.indexedChunks ?? 0) > 0 && <Chip tone="mono">{source.indexedChunks} passages</Chip>}
            <span className={shared.spacer} />
            {!editing && (
              <Button small onClick={() => setEditing(true)}>
                <Icon name="edit" size={16} /> Edit
              </Button>
            )}
            {source.kind === 'file' && (
              <Button small onClick={() => download.mutate()} disabled={download.isPending}>
                Download
              </Button>
            )}
            {source.kind === 'url' && (
              <Button small onClick={() => sync.mutate(source.id)} disabled={sync.isPending}>
                <Icon name="refresh" size={16} /> Read again
              </Button>
            )}
            <Button
              small
              variant="ghost"
              aria-label={`Delete ${source.name}`}
              onClick={async () => {
                if (!window.confirm(`Delete ${source.name}? Orbs that use it will no longer see it.`)) return
                await remove.mutateAsync(source.id)
                void navigate('/context')
              }}
            >
              <Icon name="trash" size={16} />
            </Button>
          </div>
          {source.lastError && <p className={shared.error}>{source.lastError}</p>}
          <ErrorNotice error={content.error ?? download.error ?? sync.error ?? remove.error} />
        </Card>

        {editing && content.data ? (
          <EditSource
            source={source}
            content={content.data}
            onDone={() => {
              setEditing(false)
              refresh()
            }}
          />
        ) : (
          <Card>
            {content.isPending && <Spinner />}
            {content.data && <Contents source={source} content={content.data} />}
          </Card>
        )}
      </div>
    </>
  )
}

/** The source's contents: a picture, formatted Markdown, code-like text, or the plain text orbs read. */
function Contents({ source, content }: { source: ContextSource; content: ContextContent }) {
  if (content.contentType?.startsWith('image/'))
    return <ImagePreview sourceId={source.id} name={source.name} />
  return (
    <div className={styles.contents}>
      {source.kind === 'url' && (
        <p className={shared.muted}>
          Read from{' '}
          <a href={source.location} target="_blank" rel="noopener noreferrer nofollow">
            {source.location}
          </a>
          {source.lastSyncedAt && ` · ${new Date(source.lastSyncedAt).toLocaleString()}`}
        </p>
      )}
      {content.kind === 'file' && content.editing === 'nameOnly' && (
        <p className={shared.muted}>
          This is the text Orbs read from the file. Download it to see the original.
        </p>
      )}
      {content.truncated && (
        <p className={shared.muted}>Long source: orbs read the first part, shown here.</p>
      )}
      {!content.content.trim() ? (
        <p className={shared.muted}>Nothing to show yet.</p>
      ) : isMarkdown(content) ? (
        <Markdown>{content.content}</Markdown>
      ) : codeLanguage(content) ? (
        <Markdown>{'```' + codeLanguage(content) + '\n' + content.content + '\n```'}</Markdown>
      ) : (
        <pre className={styles.plainText}>{content.content}</pre>
      )}
    </div>
  )
}

/** Loads a picture with the signed-in session and shows it. */
function ImagePreview({ sourceId, name }: { sourceId: string; name: string }) {
  const image = useQuery({
    queryKey: ['context', sourceId, 'image'],
    queryFn: () => contextApi.download(sourceId),
  })
  const url = useMemo(() => (image.data ? URL.createObjectURL(image.data) : undefined), [image.data])
  useEffect(
    () => () => {
      if (url) URL.revokeObjectURL(url)
    },
    [url],
  )
  if (image.error) return <ErrorNotice error={image.error} />
  return url ? <img className={styles.image} src={url} alt={name} /> : <Spinner />
}

/** Changes a source: its name always; a note's text, a text file's contents, or a web page's address and sync. */
function EditSource({
  source,
  content,
  onDone,
}: {
  source: ContextSource
  content: ContextContent
  onDone: () => void
}) {
  const [name, setName] = useState(source.name)
  const [text, setText] = useState(content.content)
  const [location, setLocation] = useState(source.location)
  const [syncEvery, setSyncEvery] = useState<SyncInterval>(source.syncEvery ?? 'off')
  const save = useSaveContextSource()
  const replace = useMutation({ mutationFn: () => contextApi.replaceText(source.id, text) })
  const busy = save.isPending || replace.isPending

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    await save.mutateAsync({
      id: source.id,
      body: {
        name,
        kind: source.kind,
        location: source.kind === 'url' ? location : undefined,
        input: content.editing === 'note' ? text : undefined,
        syncEvery: source.kind === 'url' ? syncEvery : undefined,
      },
    })
    if (content.editing === 'textFile' && text !== content.content) await replace.mutateAsync()
    onDone()
  }

  return (
    <Card>
      <form className={shared.stack} onSubmit={submit} aria-label={`Edit ${source.name}`}>
        <TextField
          label="Name"
          required
          maxLength={80}
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
        {source.kind === 'url' && (
          <>
            <TextField
              label="Link"
              type="url"
              required
              value={location}
              onChange={(e) => setLocation(e.target.value)}
            />
            <SelectField
              label="Keep in sync"
              value={syncEvery}
              onChange={(e) => setSyncEvery(e.target.value as SyncInterval)}
            >
              <option value="off">Only when I ask</option>
              <option value="hourly">Every hour</option>
              <option value="daily">Every day</option>
              <option value="weekly">Every week</option>
            </SelectField>
          </>
        )}
        {(content.editing === 'note' || content.editing === 'textFile') && (
          <TextArea
            label={content.editing === 'note' ? 'Content' : `Contents of ${content.fileName ?? 'the file'}`}
            required
            rows={18}
            className={styles.editor}
            value={text}
            onChange={(e) => setText(e.target.value)}
            hint={
              isMarkdown(content)
                ? 'Markdown works: # headings, **bold**, - lists, tables and code.'
                : undefined
            }
          />
        )}
        {content.editing === 'nameOnly' && source.kind === 'file' && (
          <p className={shared.muted}>
            PDFs, Word documents and pictures cannot be edited here. Download the file, change it, and upload
            it again.
          </p>
        )}
        <ErrorNotice error={save.error ?? replace.error} />
        <div className={shared.actions}>
          <Button type="submit" variant="primary" disabled={busy || !name.trim()}>
            {busy ? 'Saving…' : 'Save changes'}
          </Button>
          <Button variant="ghost" onClick={onDone}>
            Cancel
          </Button>
        </div>
      </form>
    </Card>
  )
}
