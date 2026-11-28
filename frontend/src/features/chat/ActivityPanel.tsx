import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { skillsApi } from '@/api/endpoints'
import { keys } from '@/api/queries'
import type { ContextSource, Orb } from '@/api/types'
import { Icon } from '@/components/ui/icons'
import type { ActivityEntry, ToolActivity } from './useChat'
import styles from './chat.module.css'
import { MemoryTab } from './MemoryTab'

const KIND_LABEL: Record<string, string> = { text: 'Note', url: 'Web page', file: 'File' }

export const kindLabel = (kind: string) => KIND_LABEL[kind] ?? kind

function when(at: number) {
  const seconds = Math.round((Date.now() - at) / 1000)
  if (seconds < 60) return 'just now'
  return new Date(at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

const duration = (entry: ActivityEntry) =>
  entry.finishedAt ? `${((entry.finishedAt - entry.startedAt) / 1000).toFixed(1)} s` : undefined

function Item({
  tone,
  title,
  detail,
}: {
  tone: 'ok' | 'busy' | 'err' | 'dim'
  title: string
  detail: string
}) {
  const mark = { ok: '✓', busy: '…', err: '!', dim: '·' }[tone]
  return (
    <li className={styles.act}>
      <span className={styles.actIcon} data-tone={tone} aria-hidden="true">
        {mark}
      </span>
      <span className={styles.actText}>
        <span>{title}</span>
        <span className={styles.actDetail}>{detail}</span>
      </span>
    </li>
  )
}

/** "expression: 6*7" from the JSON arguments, short enough for one line. */
function describeArguments(json: string): string {
  try {
    const value = JSON.parse(json) as Record<string, unknown>
    const parts = Object.entries(value).map(
      ([k, v]) => `${k}: ${typeof v === 'string' ? v : JSON.stringify(v)}`,
    )
    return parts.join(', ') || 'no input'
  } catch {
    return json
  }
}

const clip = (text: string, max = 120) => (text.length > max ? `${text.slice(0, max - 1)}…` : text)

function ToolItem({ call }: { call: ToolActivity }) {
  const input = describeArguments(call.arguments)
  if (call.result === undefined) return <Item tone="busy" title={`Using ${call.tool}`} detail={clip(input)} />
  return (
    <Item
      tone={call.isError ? 'err' : 'ok'}
      title={
        call.decision === 'denied'
          ? `You declined ${call.tool}`
          : call.isError
            ? `${call.tool} failed`
            : `Used ${call.tool}${call.decision === 'approved' ? ' (you approved)' : ''}`
      }
      detail={clip(`${input} → ${call.result}`)}
    />
  )
}

/** Saves the tools a finished reply used as a skill any orb can repeat. */
function SaveAsSkill({ conversationId, messageId }: { conversationId: string; messageId: string }) {
  const client = useQueryClient()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const save = useMutation({
    mutationFn: () => skillsApi.fromReply(conversationId, messageId, name),
    onSuccess: () => void client.invalidateQueries({ queryKey: keys.skills }),
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    save.mutate()
  }

  if (save.isSuccess)
    return (
      <li className={styles.actDetail}>
        Saved as the skill <Link to="/skills">{save.data.name}</Link>.
      </li>
    )
  if (!open)
    return (
      <li>
        <button type="button" className={styles.actLink} onClick={() => setOpen(true)}>
          Save these steps as a skill
        </button>
      </li>
    )
  return (
    <li>
      <form className={styles.actForm} onSubmit={submit} aria-label="Save as skill">
        <input
          className={styles.actInput}
          aria-label="Skill name"
          placeholder="Skill name"
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
        <button type="submit" className={styles.actLink} disabled={save.isPending}>
          Save
        </button>
      </form>
      {save.error && <span className={styles.actDetail}>{save.error.message}</span>}
    </li>
  )
}

function Reply({ entry, conversationId }: { entry: ActivityEntry; conversationId?: string }) {
  const done = !!entry.finishedAt
  const repeatable =
    done &&
    !entry.failed &&
    entry.tools.some((t) => t.result !== undefined && !t.isError && t.decision !== 'denied')
  const where = [entry.provider, duration(entry)].filter(Boolean).join(' · ')
  return (
    <>
      {entry.failed ? (
        <Item
          tone="err"
          title={`${entry.model} could not answer`}
          detail={entry.provider ?? 'See the message in the chat'}
        />
      ) : done ? (
        <Item tone="ok" title={`Answered with ${entry.model}`} detail={where} />
      ) : (
        <Item tone="busy" title={`Writing with ${entry.model}`} detail={entry.provider ?? 'Working on it'} />
      )}
      {[...entry.tools].reverse().map((call) => (
        <ToolItem key={call.callId} call={call} />
      ))}
      {[...entry.sources].reverse().map((s) => (
        <Item
          key={s.id}
          tone="ok"
          title={`Read ${s.name}`}
          detail={`${kindLabel(s.kind)} · ${
            s.passages
              ? `${s.passages} matching ${s.passages === 1 ? 'passage' : 'passages'}, `
              : s.truncated
                ? 'first part, '
                : ''
          }${when(entry.startedAt)}`}
        />
      ))}
      {repeatable && conversationId && <SaveAsSkill conversationId={conversationId} messageId={entry.id} />}
      {entry.sources.length === 0 && entry.tools.length === 0 && (
        <Item tone="dim" title="No sources read" detail="Answered from the model alone" />
      )}
    </>
  )
}

interface ActivityPanelProps {
  orb: Orb | undefined
  activity: ActivityEntry[]
  known: ContextSource[]
  /** The saved chat, when there is one: replies in it can be saved as skills. */
  conversationId?: string
  open: boolean
  onClose: () => void
}

/** What the orb did: per reply, the sources it read and the model that wrote it. Newest on top. */
export function ActivityPanel({ orb, activity, known, conversationId, open, onClose }: ActivityPanelProps) {
  const [tab, setTab] = useState<'activity' | 'memory'>('activity')
  return (
    <aside className={styles.activity} data-open={open} aria-label="Activity">
      <div className={styles.activityHead}>
        <div className={styles.panelTabs} role="tablist" aria-label="Panel">
          <button
            type="button"
            role="tab"
            aria-selected={tab === 'activity'}
            className={styles.panelTab}
            onClick={() => setTab('activity')}
          >
            Activity
          </button>
          {orb && (
            <button
              type="button"
              role="tab"
              aria-selected={tab === 'memory'}
              className={styles.panelTab}
              onClick={() => setTab('memory')}
            >
              Memory
            </button>
          )}
        </div>
        <button type="button" className={styles.panelClose} onClick={onClose} aria-label="Close activity">
          <Icon name="close" size={18} />
        </button>
      </div>

      {tab === 'memory' && orb ? (
        <MemoryTab orb={orb} />
      ) : (
        <ActivityContent orb={orb} activity={activity} known={known} conversationId={conversationId} />
      )}
    </aside>
  )
}

function ActivityContent({
  orb,
  activity,
  known,
  conversationId,
}: Pick<ActivityPanelProps, 'orb' | 'activity' | 'known' | 'conversationId'>) {
  return (
    <>
      {activity.length === 0 ? (
        <p className={styles.activityEmpty}>
          When {orb?.name ?? 'your orb'} answers, you see here what it read, which tools it used and which
          model wrote the reply.
        </p>
      ) : (
        <ol className={styles.actList} reversed>
          {[...activity].reverse().map((entry) => (
            <Reply key={entry.id} entry={entry} conversationId={conversationId} />
          ))}
        </ol>
      )}

      <div className={styles.knowsBox}>
        <h2 className={styles.panelLabel}>Can read</h2>
        {known.length === 0 ? (
          <p className={styles.activityEmpty}>Nothing yet.</p>
        ) : (
          <ul className={styles.knowsList}>
            {known.map((s) => (
              <li key={s.id}>
                <span className={styles.knowsDot} data-status={s.status} aria-hidden="true" />
                <span className={styles.knowsName}>{s.name}</span>
                <span className={styles.actDetail}>
                  {s.status === 'ready' ? kindLabel(s.kind) : s.status}
                </span>
              </li>
            ))}
          </ul>
        )}
        {orb && (
          <Link to={`/orbs/${orb.id}`} className={styles.knowsLink}>
            Choose what {orb.name} can read
          </Link>
        )}
      </div>
    </>
  )
}
