import type { Approval, ApprovalDecision } from '@/api/types'
import { Button } from '@/components/ui'
import styles from './chat.module.css'

function pretty(json: string): string {
  try {
    return JSON.stringify(JSON.parse(json), null, 2)
  } catch {
    return json
  }
}

/** A tool call that waits for you. The reply continues once you answer. */
export function ApprovalCard({
  orbName,
  approval,
  onDecide,
}: {
  orbName: string
  approval: Approval
  onDecide: (decision: ApprovalDecision) => void
}) {
  return (
    <section className={styles.approval} aria-label={`${orbName} wants to use ${approval.tool}`}>
      <strong>
        {orbName} wants to use <span className="mono">{approval.tool}</span>
      </strong>
      <pre>{pretty(approval.arguments)}</pre>
      <div className={styles.approvalActions}>
        <Button variant="primary" small onClick={() => onDecide('approve')}>
          Allow once
        </Button>
        <Button small onClick={() => onDecide('always')}>
          Always allow
        </Button>
        <Button small variant="ghost" onClick={() => onDecide('deny')}>
          Decline
        </Button>
      </div>
    </section>
  )
}
