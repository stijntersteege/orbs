import { Link } from 'react-router'
import type { ContextSource, Orb } from '@/api/types'
import { Icon } from '@/components/ui/icons'
import type { ActivityEntry, ToolActivity } from './useChat'
import styles from './chat.module.css'

const KIND_LABEL: Record<string, string> = { text: 'Note', url: 'Web page' }

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
      title={call.isError ? `${call.tool} failed` : `Used ${call.tool}`}
      detail={clip(`${input} → ${call.result}`)}
    />
  )
}

function Reply({ entry }: { entry: ActivityEntry }) {
  const done = !!entry.finishedAt
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
          detail={`${kindLabel(s.kind)} · ${s.truncated ? 'first part, ' : ''}${when(entry.startedAt)}`}
        />
      ))}
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
  open: boolean
  onClose: () => void
}

/** What the orb did: per reply, the sources it read and the model that wrote it. Newest on top. */
export function ActivityPanel({ orb, activity, known, open, onClose }: ActivityPanelProps) {
  return (
    <aside className={styles.activity} data-open={open} aria-label="Activity">
      <div className={styles.activityHead}>
        <h2 className={styles.panelLabel}>Activity</h2>
        <button type="button" className={styles.panelClose} onClick={onClose} aria-label="Close activity">
          <Icon name="close" size={18} />
        </button>
      </div>

      {activity.length === 0 ? (
        <p className={styles.activityEmpty}>
          When {orb?.name ?? 'your orb'} answers, you see here what it read, which tools it used and which
          model wrote the reply.
        </p>
      ) : (
        <ol className={styles.actList} reversed>
          {[...activity].reverse().map((entry) => (
            <Reply key={entry.id} entry={entry} />
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
    </aside>
  )
}
