import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { conversationsApi } from '@/api/endpoints'
import { keys, useContextSources, useConversation, useConversations, useOrb } from '@/api/queries'
import { Orb } from '@/components/orb/Orb'
import { Button, Chip, ErrorNotice, Spinner } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import { useChat } from './useChat'
import { plural } from '@/lib/format'
import styles from './chat.module.css'

/** Talk to one orb. Conversations are listed on the side; replies stream in as they are written. */
export function ChatPage() {
  const { orbId, conversationId } = useParams()
  const navigate = useNavigate()
  const client = useQueryClient()
  const orb = useOrb(orbId)
  const sources = useContextSources()
  const conversations = useConversations(orbId)
  const conversation = useConversation(conversationId)
  const chat = useChat(conversationId, conversation.data?.messages)
  const [draft, setDraft] = useState('')
  const [startError, setStartError] = useState<unknown>(null)
  const endRef = useRef<HTMLDivElement>(null)

  // Keep the newest text in view whenever a message arrives or the reply grows.
  const lastActivity = `${chat.messages.length}:${chat.streaming?.length ?? -1}`
  useEffect(() => {
    if (lastActivity) endRef.current?.scrollIntoView?.({ block: 'end' })
  }, [lastActivity])

  if (orb.isPending) return <Spinner />
  if (orb.error || !orb.data) return <ErrorNotice error={orb.error ?? new Error('Orb not found.')} />

  const known = sources.data?.filter((s) => orb.data.contextSourceIds.includes(s.id)) ?? []

  const submit = async (e?: FormEvent) => {
    e?.preventDefault()
    const text = draft.trim()
    if (!text || chat.isStreaming) return
    setDraft('')
    setStartError(null)
    let targetId = conversationId
    if (!targetId) {
      try {
        const started = await conversationsApi.start(orb.data.id)
        targetId = started.id
        client.setQueryData(keys.conversation(started.id), started)
        navigate(`/orbs/${orb.data.id}/chat/${started.id}`, { replace: true })
      } catch (error) {
        setStartError(error)
        setDraft(text)
        return
      }
    }
    await chat.send(text, targetId)
  }

  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      void submit()
    }
  }

  return (
    <div className={styles.layout}>
      <aside className={styles.history} aria-label="Conversations">
        <Button variant="primary" onClick={() => navigate(`/orbs/${orb.data.id}/chat`)}>
          <Icon name="plus" size={18} /> New chat
        </Button>
        <ul className={styles.historyList}>
          {conversations.data?.map((c) => (
            <li key={c.id}>
              <Link
                to={`/orbs/${orb.data.id}/chat/${c.id}`}
                className={styles.historyItem}
                aria-current={c.id === conversationId ? 'page' : undefined}
              >
                {c.title}
              </Link>
            </li>
          ))}
        </ul>
      </aside>

      <section className={styles.chat} aria-label={`Chat with ${orb.data.name}`}>
        <header className={styles.header}>
          <Link to="/" className={styles.back} aria-label="Back to orbs">
            <Icon name="back" />
          </Link>
          <Orb appearance={orb.data.appearance} size={48} thinking={chat.isStreaming} />
          <div className={styles.headerText}>
            <h1 className={styles.title}>{orb.data.name}</h1>
            <span className={styles.meta}>
              {known.length > 0 ? `● Knows ${plural(known.length, 'source')}` : 'No context linked'}
            </span>
          </div>
          <Chip tone="mono">{chat.model ?? orb.data.model?.model ?? 'auto'}</Chip>
          <Link to={`/orbs/${orb.data.id}`} className={styles.back} aria-label={`Set up ${orb.data.name}`}>
            <Icon name="edit" size={18} />
          </Link>
        </header>

        <div className={styles.messages} aria-live="polite">
          {conversation.isFetching && !conversation.data && <Spinner />}
          {chat.messages.length === 0 && !chat.isStreaming && (
            <div className={styles.welcome}>
              <Orb appearance={orb.data.appearance} size={120} />
              <p>
                Say hi to <strong>{orb.data.name}</strong>.
              </p>
              {orb.data.description && <p className={styles.meta}>{orb.data.description}</p>}
            </div>
          )}
          {chat.messages.map((m) => (
            <div key={m.id} className={m.role === 'user' ? styles.user : styles.assistant}>
              {m.content}
            </div>
          ))}
          {chat.streaming !== null && (
            <div className={styles.assistant} aria-busy="true">
              {chat.streaming || <span className={styles.meta}>Thinking…</span>}
            </div>
          )}
          {known.length > 0 && chat.messages.some((m) => m.role === 'assistant') && (
            <div className={styles.sources}>
              {known.map((s) => (
                <Chip key={s.id}>{s.name}</Chip>
              ))}
            </div>
          )}
          <div ref={endRef} />
        </div>

        <ErrorNotice error={startError ?? (chat.error ? new Error(chat.error) : null)} />

        <form className={styles.composer} onSubmit={submit}>
          <label htmlFor="message" className="visually-hidden">
            Message {orb.data.name}
          </label>
          <textarea
            id="message"
            className={styles.input}
            rows={1}
            value={draft}
            placeholder={`Message ${orb.data.name}`}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={onKeyDown}
          />
          {chat.isStreaming ? (
            <Button variant="secondary" iconOnly onClick={chat.stop} aria-label="Stop">
              <Icon name="stop" />
            </Button>
          ) : (
            <Button type="submit" variant="primary" iconOnly disabled={!draft.trim()} aria-label="Send">
              <Icon name="send" />
            </Button>
          )}
        </form>
      </section>
    </div>
  )
}
