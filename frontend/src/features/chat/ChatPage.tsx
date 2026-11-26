import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { conversationsApi } from '@/api/endpoints'
import {
  keys,
  useContextSources,
  useConversation,
  useConversations,
  useOrbs,
  useProviders,
  useSaveOrb,
} from '@/api/queries'
import { DEFAULT_APPEARANCE, type ChatSource, type ModelRef, type Orb as OrbModel } from '@/api/types'
import { Orb } from '@/components/orb/Orb'
import { ErrorNotice, Spinner } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import { useSettings } from '@/app/SettingsContext'
import { useChat } from './useChat'
import { ModelPicker } from './ModelPicker'
import { ActivityPanel, kindLabel } from './ActivityPanel'
import { usePopover } from '@/lib/usePopover'
import styles from './chat.module.css'

function Cites({ sources }: { sources?: ChatSource[] }) {
  if (!sources || sources.length === 0) return null
  return (
    <span className={styles.cites} aria-label="Sources read">
      {sources.map((s) => (
        <span key={s.id}>
          {kindLabel(s.kind)} · {s.name}
        </span>
      ))}
    </span>
  )
}

function ChatsMenu({ orb, current }: { orb: OrbModel; current?: string }) {
  const conversations = useConversations(orb.id)
  const popover = usePopover()
  const recent = conversations.data?.slice(0, 8) ?? []
  return (
    <div className={styles.popoverWrap} ref={popover.ref}>
      <button
        type="button"
        className={styles.headerButton}
        aria-expanded={popover.open}
        aria-haspopup="true"
        aria-label="Chats"
        onClick={popover.toggle}
      >
        <Icon name="chat" size={16} />
        <span className={styles.buttonLabel}>Chats</span>
      </button>
      {popover.open && (
        <div className={[styles.popover, styles.alignEnd, styles.below].join(' ')}>
          <div className={styles.orbMenu} role="menu" aria-label={`Chats with ${orb.name}`}>
            <Link
              to={`/orbs/${orb.id}/chat`}
              className={styles.modelOption}
              role="menuitem"
              onClick={popover.close}
            >
              <Icon name="plus" size={16} />
              <span className={styles.modelName}>New chat</span>
            </Link>
            {recent.length > 0 && <span className={styles.modelGroupLabel}>Recent</span>}
            {recent.map((c) => (
              <Link
                key={c.id}
                to={`/orbs/${orb.id}/chat/${c.id}`}
                role="menuitem"
                aria-current={c.id === current ? 'page' : undefined}
                className={styles.modelOption}
                onClick={popover.close}
              >
                <span className={styles.modelName}>{c.title}</span>
              </Link>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

/**
 * The home screen: one conversation, a model picker on top and a message box.
 * The chat belongs to an orb (its face, context and instructions); the model can be switched any time.
 */
export function ChatPage() {
  const params = useParams()
  const navigate = useNavigate()
  const client = useQueryClient()
  const { settings, update } = useSettings()
  const orbs = useOrbs()
  const providers = useProviders()
  const sources = useContextSources()
  const saveOrb = useSaveOrb()

  const orb =
    orbs.data?.find((o) => o.id === params.orbId) ??
    (params.orbId ? undefined : (orbs.data?.find((o) => o.id === settings.lastOrbId) ?? orbs.data?.[0]))
  const conversationId = params.conversationId
  const conversation = useConversation(conversationId)
  const chat = useChat(conversationId, conversation.data?.messages)
  const [draft, setDraft] = useState('')
  const [pendingModel, setPendingModel] = useState<ModelRef | undefined>()
  const [error, setError] = useState<unknown>(null)
  const [activityOpen, setActivityOpen] = useState(false)
  const endRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLTextAreaElement>(null)

  // A new chat starts with the orb you used last.
  useEffect(() => {
    if (orb && orb.id !== settings.lastOrbId) update({ lastOrbId: orb.id })
  }, [orb, settings.lastOrbId, update])

  // Keep the newest text in view whenever a message arrives or the reply grows.
  const lastActivity = `${chat.messages.length}:${chat.streaming?.length ?? -1}`
  useEffect(() => {
    if (lastActivity) endRef.current?.scrollIntoView?.({ block: 'end' })
  }, [lastActivity])

  if (orbs.isPending || providers.isPending) return <Spinner />
  if (orbs.error) return <ErrorNotice error={orbs.error} />
  if (params.orbId && !orb) return <ErrorNotice error={new Error('Orb not found.')} />

  const allProviders = providers.data ?? []
  const hasModel = allProviders.some((p) => p.isEnabled)
  const known = orb ? (sources.data?.filter((s) => orb.contextSourceIds.includes(s.id)) ?? []) : []
  const model = orb ? orb.model : pendingModel
  const name = orb?.name ?? 'Pip'
  const citations = new Map(chat.activity.map((a) => [a.id, a.sources]))
  const empty = chat.messages.length === 0 && !chat.isStreaming && !conversation.isFetching

  const pickModel = (ref: ModelRef | null) => {
    if (!orb) {
      setPendingModel(ref ?? undefined)
      return
    }
    saveOrb.mutate({
      id: orb.id,
      body: {
        name: orb.name,
        description: orb.description,
        instructions: orb.instructions,
        appearance: orb.appearance,
        contextSourceIds: orb.contextSourceIds,
        model: ref,
      },
    })
  }

  const submit = async (e?: FormEvent) => {
    e?.preventDefault()
    const text = draft.trim()
    if (!text || chat.isStreaming) return
    setDraft('')
    if (inputRef.current) inputRef.current.style.height = 'auto'
    setError(null)
    let targetId = conversationId
    try {
      // First run without any orb: make a default one, so people can just start typing.
      const target =
        orb ??
        (await saveOrb.mutateAsync({
          body: {
            name: 'Pip',
            description: 'Your first orb',
            appearance: DEFAULT_APPEARANCE,
            model: pendingModel ?? null,
          },
        }))
      if (!targetId) {
        const started = await conversationsApi.start(target.id)
        targetId = started.id
        chat.adopt(started.id)
        client.setQueryData(keys.conversation(started.id), started)
        void client.invalidateQueries({ queryKey: ['conversations'] })
        navigate(`/orbs/${target.id}/chat/${started.id}`, { replace: !!params.orbId })
      }
    } catch (err) {
      setError(err)
      setDraft(text)
      return
    }
    await chat.send(text, targetId)
  }

  // Grow the message box with its text, up to a limit.
  const onDraft = (el: HTMLTextAreaElement) => {
    setDraft(el.value)
    el.style.height = 'auto'
    el.style.height = `${Math.min(el.scrollHeight, 200)}px`
  }

  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      void submit()
    }
  }

  const composer = (
    <form className={styles.composer} onSubmit={submit}>
      <label htmlFor="message" className="visually-hidden">
        Message {name}
      </label>
      <textarea
        id="message"
        ref={inputRef}
        className={styles.input}
        rows={1}
        value={draft}
        placeholder={hasModel ? `Message ${name}` : 'Connect a model to start'}
        onChange={(e) => onDraft(e.target)}
        onKeyDown={onKeyDown}
      />
      <div className={styles.composerBar}>
        <span className={styles.composerHint}>
          {orb
            ? `${orb.name} answers with ${chat.model ?? model?.model ?? 'Auto'}`
            : 'Your first orb is made when you send'}
        </span>
        {chat.isStreaming ? (
          <button type="button" className={styles.send} onClick={chat.stop} aria-label="Stop">
            <Icon name="stop" size={16} />
          </button>
        ) : (
          <button type="submit" className={styles.send} disabled={!draft.trim()} aria-label="Send">
            <Icon name="up" size={18} />
          </button>
        )}
      </div>
    </form>
  )

  return (
    <div className={styles.layout}>
      <div className={styles.page}>
        <header className={styles.header}>
          {orb && (
            <Orb
              size={28}
              appearance={{ ...orb.appearance, glow: false, floating: false, accessory: 'none' }}
            />
          )}
          <h1 className={styles.orbTitle}>{name}</h1>
          <ModelPicker
            providers={allProviders}
            value={model}
            answeredWith={chat.model}
            onChange={pickModel}
            disabled={saveOrb.isPending}
          />
          {known.length > 0 && (
            <span className={styles.headerSources}>· {known.map((s) => s.name).join(', ')}</span>
          )}
          <span className={styles.spacer} />
          {orb && <ChatsMenu orb={orb} current={conversationId} />}
          <button
            type="button"
            className={[styles.headerButton, styles.activityToggle].join(' ')}
            aria-expanded={activityOpen}
            aria-label="Activity"
            onClick={() => setActivityOpen((o) => !o)}
          >
            <Icon name="pulse" size={16} />
            <span className={styles.buttonLabel}>Activity</span>
          </button>
          {orb && (
            <Link to={`/orbs/${orb.id}`} className={styles.headerLink} aria-label={`Set up ${orb.name}`}>
              <Icon name="settings" size={18} />
            </Link>
          )}
        </header>

        {empty ? (
          <div className={styles.start}>
            <Orb appearance={orb?.appearance ?? DEFAULT_APPEARANCE} size={72} />
            <h2 className={styles.greeting}>
              {orb ? `What can ${orb.name} help with?` : 'What can I help with?'}
            </h2>
            {!hasModel && (
              <p className={styles.startHint}>
                Orbs runs on the model you choose. <Link to="/models">Connect a model</Link> to start:
                OpenRouter, OpenAI, a local Ollama or any compatible endpoint.
              </p>
            )}
            <ErrorNotice error={error ?? (chat.error ? new Error(chat.error) : null)} />
            {composer}
          </div>
        ) : (
          <>
            <div className={styles.messages} aria-live="polite">
              <div className={styles.thread}>
                {conversation.isFetching && !conversation.data && <Spinner />}
                {chat.messages.map((m) =>
                  m.role === 'user' ? (
                    <div key={m.id} className={styles.user}>
                      {m.content}
                    </div>
                  ) : (
                    <div key={m.id} className={styles.assistantRow}>
                      <Orb
                        size={28}
                        appearance={{
                          ...(orb?.appearance ?? DEFAULT_APPEARANCE),
                          floating: false,
                          glow: false,
                        }}
                      />
                      <div className={styles.assistant}>
                        {m.content}
                        <Cites sources={citations.get(m.id)} />
                      </div>
                    </div>
                  ),
                )}
                {chat.streaming !== null && (
                  <div className={styles.assistantRow}>
                    <Orb
                      size={28}
                      thinking
                      appearance={{
                        ...(orb?.appearance ?? DEFAULT_APPEARANCE),
                        floating: false,
                        glow: false,
                      }}
                    />
                    <div className={styles.assistant} aria-busy="true">
                      {chat.streaming || <span className={styles.thinking}>Thinking…</span>}
                    </div>
                  </div>
                )}
                <div ref={endRef} />
              </div>
            </div>
            <div className={styles.dock}>
              <ErrorNotice error={error ?? (chat.error ? new Error(chat.error) : null)} />
              {composer}
            </div>
          </>
        )}
      </div>
      <ActivityPanel
        orb={orb}
        activity={chat.activity}
        known={known}
        open={activityOpen}
        onClose={() => setActivityOpen(false)}
      />
    </div>
  )
}
