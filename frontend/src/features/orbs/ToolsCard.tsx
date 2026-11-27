import { useTools } from '@/api/queries'
import type { ToolAccess } from '@/api/types'
import { Card, Chip } from '@/components/ui'
import styles from './orbs.module.css'

const ACCESS: { id: ToolAccess; label: string }[] = [
  { id: 'allow', label: 'Allow' },
  { id: 'ask', label: 'Ask first' },
  { id: 'block', label: 'Block' },
]

interface ToolsCardProps {
  name: string
  step: number
  permissions: Record<string, ToolAccess>
  onChange: (permissions: Record<string, ToolAccess>) => void
}

/** Per tool: may the orb use it freely, ask first (the default), or never. */
export function ToolsCard({ name, step, permissions, onChange }: ToolsCardProps) {
  const { data: tools } = useTools()

  const set = (tool: string, access: ToolAccess) => {
    const next = { ...permissions }
    if (access === 'ask') delete next[tool]
    else next[tool] = access
    onChange(next)
  }

  return (
    <Card>
      <div className={styles.sectionHead}>
        <span className={styles.step}>{step}</span>
        <h2>What {name} can do</h2>
      </div>
      <p className={styles.muted} style={{ marginBottom: 16 }}>
        Tools let {name} look things up and act in other apps. By default it asks you in the chat before every
        call.
      </p>
      {tools?.length === 0 && <p className={styles.muted}>No tools are connected yet.</p>}
      <ul className={styles.checkList} aria-label="Tools">
        {tools?.map((tool) => {
          const access = permissions[tool.name] ?? 'ask'
          return (
            <li key={tool.name} className={styles.toolRow}>
              <span className={styles.checkText}>
                <strong>
                  {tool.displayName} {tool.risk === 'write' && <Chip tone="warn">acts</Chip>}
                </strong>
                <span className={styles.muted}>{tool.description}</span>
              </span>
              <select
                aria-label={`${tool.displayName} access`}
                className={styles.accessSelect}
                value={access}
                onChange={(e) => set(tool.name, e.target.value as ToolAccess)}
              >
                {ACCESS.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.label}
                  </option>
                ))}
              </select>
            </li>
          )
        })}
      </ul>
    </Card>
  )
}
