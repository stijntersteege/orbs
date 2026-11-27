import { useJobs } from '@/api/queries'
import type { Job } from '@/api/types'
import { Card, Chip, ErrorNotice } from '@/components/ui'
import shared from '../shared.module.css'

const TONE: Record<Job['status'], 'neutral' | 'ok' | 'warn' | 'err'> = {
  pending: 'neutral',
  running: 'warn',
  succeeded: 'ok',
  failed: 'err',
}

const when = (iso: string) =>
  new Date(iso).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })

/** What ran in the background for you: schedules, syncs and housekeeping. */
export function JobsCard() {
  const { data: jobs, error } = useJobs()
  return (
    <Card>
      <div className={shared.stack}>
        <h2>Background work</h2>
        <ErrorNotice error={error} />
        {jobs && jobs.length === 0 && <p className={shared.muted}>Nothing has run in the background yet.</p>}
        {jobs && jobs.length > 0 && (
          <ul className={shared.list} aria-label="Background jobs">
            {jobs.map((job) => (
              <li key={job.id} className={shared.row}>
                <span className={shared.rowText}>
                  <span className="mono">{job.kind}</span>
                  <span className={shared.muted}>
                    {job.status === 'pending'
                      ? `Next run ${when(job.runAt)}`
                      : when(job.finishedAt ?? job.runAt)}
                    {job.result ? ` · ${job.result}` : ''}
                    {job.lastError ? ` · ${job.lastError}` : ''}
                  </span>
                </span>
                <Chip tone={TONE[job.status]}>{job.status}</Chip>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Card>
  )
}
