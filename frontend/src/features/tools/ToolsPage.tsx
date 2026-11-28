import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { mcpApi } from '@/api/endpoints'
import { keys, useMcpServers, useTools } from '@/api/queries'
import type { McpServer, McpServerTools } from '@/api/types'
import { Button, Card, Chip, ErrorNotice, PageHeader, Spinner, TextField } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import shared from '../shared.module.css'
import { IntegrationsCard } from './IntegrationsCard'

/** Everything orbs can do: built-in tools and the tools of connected MCP servers. */
export function ToolsPage() {
  const tools = useTools()
  const servers = useMcpServers()
  const client = useQueryClient()
  const refresh = () => {
    void client.invalidateQueries({ queryKey: keys.mcpServers })
    void client.invalidateQueries({ queryKey: keys.tools })
    void client.invalidateQueries({ queryKey: keys.integrations })
  }

  return (
    <>
      <PageHeader
        title="Tools"
        subtitle="What your orbs can do. Each orb asks before using a tool unless you allow it in the orb's settings."
      />
      <div className={shared.stack} style={{ maxWidth: 760 }}>
        <IntegrationsCard onChange={refresh} />

        <Card>
          <div className={shared.stack}>
            <h2>MCP servers</h2>
            <p className={shared.muted}>
              Connect any Model Context Protocol server: its tools show up for every orb.
            </p>
            <ErrorNotice error={servers.error} />
            {servers.data?.map((s) => (
              <ServerRow key={s.id} server={s} onChange={refresh} />
            ))}
            <AddServer onAdded={refresh} />
          </div>
        </Card>

        <Card>
          <div className={shared.stack}>
            <h2>Available tools</h2>
            {tools.isPending && <Spinner />}
            <ul className={shared.list} aria-label="Available tools">
              {tools.data?.map((t) => (
                <li key={t.name} className={shared.row}>
                  <span className={shared.rowText}>
                    <strong>{t.displayName}</strong>
                    <span className={shared.muted}>{t.description}</span>
                  </span>
                  <Chip tone={t.risk === 'write' ? 'warn' : 'neutral'}>
                    {t.risk === 'write' ? 'acts' : 'reads'}
                  </Chip>
                </li>
              ))}
            </ul>
          </div>
        </Card>
      </div>
    </>
  )
}

function ServerRow({ server, onChange }: { server: McpServer; onChange: () => void }) {
  const [check, setCheck] = useState<McpServerTools | null>(null)
  const test = useMutation({
    mutationFn: () => mcpApi.tools(server.id),
    onSuccess: (r) => (setCheck(r), onChange()),
  })
  const remove = useMutation({ mutationFn: () => mcpApi.remove(server.id), onSuccess: onChange })
  const error = check?.error ?? server.lastError

  return (
    <div className={shared.row}>
      <span className={shared.rowText}>
        <strong>{server.name}</strong>
        <span className={`${shared.muted} mono`}>{server.location}</span>
        {check && !check.error && (
          <span className={shared.muted}>
            {check.tools.map((t) => t.displayName).join(', ') || 'No tools'}
          </span>
        )}
        {error && <span className={shared.error}>{error}</span>}
      </span>
      <Button small onClick={() => test.mutate()} disabled={test.isPending}>
        Test
      </Button>
      <Button small variant="ghost" aria-label={`Remove ${server.name}`} onClick={() => remove.mutate()}>
        <Icon name="trash" />
      </Button>
    </div>
  )
}

function AddServer({ onAdded }: { onAdded: () => void }) {
  const [name, setName] = useState('')
  const [url, setUrl] = useState('')
  const [auth, setAuth] = useState('')
  const add = useMutation({
    mutationFn: () =>
      mcpApi.create({ name, transport: 'http', location: url, authorization: auth || undefined }),
    onSuccess: () => {
      setName('')
      setUrl('')
      setAuth('')
      onAdded()
    },
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    add.mutate()
  }
  return (
    <form className={shared.stack} onSubmit={submit} aria-label="Connect an MCP server">
      <div className={shared.grid2}>
        <TextField
          label="Name"
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Home"
        />
        <TextField
          label="Server URL"
          type="url"
          required
          value={url}
          onChange={(e) => setUrl(e.target.value)}
          placeholder="https://mcp.example.com/mcp"
        />
      </div>
      <TextField
        label="Authorization header"
        type="password"
        autoComplete="off"
        value={auth}
        onChange={(e) => setAuth(e.target.value)}
        placeholder="Optional, for example: Bearer abc123"
        hint="Stored encrypted and never shown again."
      />
      <ErrorNotice error={add.error} />
      <div className={shared.actions}>
        <Button type="submit" variant="primary" disabled={add.isPending}>
          Connect server
        </Button>
      </div>
    </form>
  )
}
