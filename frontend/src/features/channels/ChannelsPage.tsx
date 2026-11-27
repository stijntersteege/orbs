import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { channelsApi } from '@/api/endpoints'
import { keys, useChannelKinds, useChannels, useOrbs } from '@/api/queries'
import type { Channel, ChannelKind, Orb as OrbModel } from '@/api/types'
import { Orb } from '@/components/orb/Orb'
import { Button, Card, Chip, ErrorNotice, PageHeader, SelectField, Spinner, TextField } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import { orbPng } from '@/lib/orbImage'
import shared from '../shared.module.css'

/** Shows how to pair a chat: send the one-time code to the orb in the app. */
function Pairing({ channel, code }: { channel: Channel; code: string }) {
  const message = channel.kind === 'telegram' ? `/start ${code}` : code
  const how =
    channel.kind === 'email' ? 'Mail' : channel.kind === 'matrix' ? 'Invite it to a room and send' : 'Send'
  return (
    <p className={shared.muted}>
      {how} <strong>{channel.address}</strong> the message <code className="mono">{message}</code>. The code
      works once; anyone else who finds the orb gets no answer.
    </p>
  )
}

/** Orbs in messaging apps: each one its own bot, answering only chats you paired. */
export function ChannelsPage() {
  const channels = useChannels()
  const kinds = useChannelKinds()
  const orbs = useOrbs()
  const client = useQueryClient()
  const refresh = () => client.invalidateQueries({ queryKey: keys.channels })
  const [pairing, setPairing] = useState<{ id: string; code: string; note?: string } | null>(null)

  return (
    <>
      <PageHeader title="Channels" subtitle="Talk to your orbs in the messaging apps you already use." />
      <div className={shared.stack} style={{ maxWidth: 760 }}>
        <Card>
          {channels.isPending && <Spinner />}
          <ErrorNotice error={channels.error} />
          {channels.data?.length === 0 && <p className={shared.muted}>No channels yet.</p>}
          <ul className={shared.list} aria-label="Channels">
            {channels.data?.map((c) => (
              <ChannelRow
                key={c.id}
                channel={c}
                orb={orbs.data?.find((o) => o.id === c.orbId)}
                kind={kinds.data?.find((k) => k.kind === c.kind)}
                pairing={pairing?.id === c.id ? pairing : undefined}
                onPairing={(code) => setPairing({ id: c.id, code })}
                onChange={refresh}
              />
            ))}
          </ul>
        </Card>
        {kinds.data && orbs.data && orbs.data.length > 0 && (
          <ConnectChannel
            kinds={kinds.data}
            orbs={orbs.data}
            onConnected={(c) => {
              if (c.pairingCode) setPairing({ id: c.id, code: c.pairingCode, note: c.note })
              void refresh()
            }}
          />
        )}
      </div>
    </>
  )
}

function ChannelRow({
  channel,
  orb,
  kind,
  pairing,
  onPairing,
  onChange,
}: {
  channel: Channel
  orb?: OrbModel
  kind?: ChannelKind
  pairing?: { code: string; note?: string }
  onPairing: (code: string) => void
  onChange: () => void
}) {
  const newCode = useMutation({
    mutationFn: () => channelsApi.pairingCode(channel.id),
    onSuccess: (c) => c.pairingCode && onPairing(c.pairingCode),
  })
  const remove = useMutation({ mutationFn: () => channelsApi.remove(channel.id), onSuccess: onChange })
  const unpair = useMutation({
    mutationFn: (chatId: string) => channelsApi.unpair(channel.id, chatId),
    onSuccess: onChange,
  })
  const avatar = useMutation({
    mutationFn: async () => channelsApi.avatar(channel.id, await orbPng(orb!.appearance)),
  })
  const download = async () => {
    const blob = await orbPng(orb!.appearance)
    const link = document.createElement('a')
    link.href = URL.createObjectURL(blob)
    link.download = `${orb!.name}.png`
    link.click()
    URL.revokeObjectURL(link.href)
  }

  return (
    <li className={shared.row} style={{ alignItems: 'flex-start' }}>
      {orb && <Orb size={40} appearance={{ ...orb.appearance, glow: false, floating: false }} />}
      <span className={shared.rowText}>
        <strong>
          {orb?.name ?? 'Orb'} <Chip>{kind?.displayName ?? channel.kind}</Chip>{' '}
          <span className="mono">{channel.address}</span>
        </strong>
        {channel.lastError && <span className={shared.error}>{channel.lastError}</span>}
        {pairing && <Pairing channel={channel} code={pairing.code} />}
        {pairing?.note && <span className={shared.muted}>{pairing.note}</span>}
        {avatar.data?.note && <span className={shared.muted}>{avatar.data.note}</span>}
        {channel.chats.length === 0 && !pairing && <span className={shared.muted}>No chat paired yet.</span>}
        {channel.chats.map((chat) => (
          <span key={chat.id} className={shared.muted}>
            Paired with {chat.name || 'a chat'}
            {chat.conversationId && (
              <>
                {' · '}
                <Link to={`/orbs/${channel.orbId}/chat/${chat.conversationId}`}>Open chat</Link>
              </>
            )}
            {' · '}
            <button type="button" className={shared.linkButton} onClick={() => unpair.mutate(chat.id)}>
              Unpair
            </button>
          </span>
        ))}
        <span className={shared.actions} style={{ marginTop: 8 }}>
          <Button small onClick={() => newCode.mutate()} disabled={newCode.isPending}>
            Pair a chat
          </Button>
          {orb && (
            <>
              <Button small variant="ghost" onClick={() => avatar.mutate()} disabled={avatar.isPending}>
                Use face as picture
              </Button>
              <Button small variant="ghost" onClick={() => void download()}>
                Download face
              </Button>
            </>
          )}
        </span>
        <ErrorNotice error={newCode.error ?? remove.error ?? unpair.error ?? avatar.error} />
      </span>
      <Button
        small
        variant="ghost"
        aria-label={`Disconnect ${channel.address ?? channel.kind}`}
        onClick={() => remove.mutate()}
      >
        <Icon name="trash" />
      </Button>
    </li>
  )
}

function ConnectChannel({
  kinds,
  orbs,
  onConnected,
}: {
  kinds: ChannelKind[]
  orbs: OrbModel[]
  onConnected: (channel: Channel) => void
}) {
  const [orbId, setOrbId] = useState(orbs[0]!.id)
  const [kindId, setKindId] = useState(kinds[0]!.kind)
  const [values, setValues] = useState<Record<string, string>>({})
  const kind = kinds.find((k) => k.kind === kindId) ?? kinds[0]!
  const secretField = kind.fields.find((f) => f.secret)

  const connect = useMutation({
    mutationFn: () =>
      channelsApi.create({
        orbId,
        kind: kind.kind,
        settings: Object.fromEntries(
          kind.fields.filter((f) => !f.secret && values[f.key]).map((f) => [f.key, values[f.key]!]),
        ),
        secret: secretField ? values[secretField.key] : undefined,
      }),
    onSuccess: (c) => {
      setValues({})
      onConnected(c)
    },
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    connect.mutate()
  }

  return (
    <Card>
      <form className={shared.stack} onSubmit={submit} aria-label="Connect a channel">
        <h2>Connect a channel</h2>
        <div className={shared.grid2}>
          <SelectField label="Orb" value={orbId} onChange={(e) => setOrbId(e.target.value)}>
            {orbs.map((o) => (
              <option key={o.id} value={o.id}>
                {o.name}
              </option>
            ))}
          </SelectField>
          <SelectField
            label="App"
            value={kind.kind}
            onChange={(e) => {
              setKindId(e.target.value)
              setValues({})
            }}
          >
            {kinds.map((k) => (
              <option key={k.kind} value={k.kind}>
                {k.displayName}
              </option>
            ))}
          </SelectField>
        </div>
        <p className={shared.muted}>{kind.description}</p>
        {kind.fields.map((field) => (
          <TextField
            key={field.key}
            label={field.label}
            type={field.secret ? 'password' : 'text'}
            autoComplete="off"
            required={field.required}
            placeholder={field.placeholder}
            hint={field.hint}
            value={values[field.key] ?? ''}
            onChange={(e) => setValues((v) => ({ ...v, [field.key]: e.target.value }))}
          />
        ))}
        <ErrorNotice error={connect.error} />
        <div className={shared.actions}>
          <Button type="submit" variant="primary" disabled={connect.isPending}>
            Connect
          </Button>
        </div>
      </form>
    </Card>
  )
}
