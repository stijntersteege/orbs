import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { conversationsApi } from '@/api/endpoints'
import { keys, useContextSources, useConversation, useOrbs, useProviders, useSaveOrb } from '@/api/queries'
import { DEFAULT_APPEARANCE, type ModelRef, type Orb as OrbModel } from '@/api/types'
import { Orb } from '@/components/orb/Orb'
import { ErrorNotice, Spinner } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import { useSettings } from '@/app/SettingsContext'
import { plural } from '@/lib/format'
import { useChat } from './useChat'
import { ModelPicker } from './ModelPicker'
import { usePopover } from '@/lib/usePopover'
import styles from './chat.module.css'

function OrbPicker({ orbs, current }: { orbs: OrbModel[]; current: OrbModel }) {
  const navigate = useNavigate()
  const popover = usePopover()
  return (
    <div className={styles.popoverWrap} ref={popover.ref}>
      <button
        type="button"
        className={styles.orbTrigger}
        aria-expanded={popover.open}
        aria-haspopup="true"
        aria-label={`Orb: ${current.name}`}
        onClick={popover.toggle}
      >
        <Orb
          size={22}
          appearance={{ ...current.appearance, glow: false, floating: false, accessory: 'none' }}
        />
        {current.name}
        <Icon name="chevron" size={14} />
      </button>
      {popover.open && (
        <div className={[styles.popover, styles.alignStart, styles.above].join(' ')}>
          <div className={styles.orbMenu} role="menu" aria-label="Orbs">
            {orbs.map((o) => (
              <button
                key={o.id}
                type="button"
                role="menuitemradio"
                aria-checked={o.id === current.id}
                className={styles.modelOption}
                onClick={() => {
                  popover.close()
                  navigate(`/orbs/${o.id}/chat`)
                }}
              >
                <Orb
                  size={24}
                  appearance={{ ...o.appearance, glow: false, floating: false, accessory: 'none' }}
                />
                <span className={styles.modelName}>
                  {o.name}
                  {o.description && <span className={styles.modelHint}>{o.description}</span>}
                </span>
                {o.id === current.id && <Icon name="check" size={16} />}
              </button>
            ))}
            <Link to="/orbs/new" className={styles.modelManage}>
              <Icon name="plus" size={16} /> New orb
            </Link>
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
        {orb && orbs.data && <OrbPicker orbs={orbs.data} current={orb} />}
        {known.length > 0 && (
          <span className={styles.knows} title={known.map((s) => s.name).join(', ')}>
            <Icon name="context" size={14} /> {plural(known.length, 'source')}
          </span>
        )}
        <span className={styles.spacer} />
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
    <div className={styles.page}>
      <header className={styles.header}>
        <ModelPicker
          providers={allProviders}
          value={model}
          answeredWith={chat.model}
          onChange={pickModel}
          disabled={saveOrb.isPending}
        />
        {orb && (
          <Link to={`/orbs/${orb.id}`} className={styles.headerLink} aria-label={`Set up ${orb.name}`}>
            <Icon name="more" />
          </Link>
        )}
      </header>

      {empty ? (
        <div className={styles.start}>
          <Orb appearance={orb?.appearance ?? DEFAULT_APPEARANCE} size={72} />
          <h1 className={styles.greeting}>
            {orb ? `What can ${orb.name} help with?` : 'What can I help with?'}
          </h1>
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
                    <div className={styles.assistant}>{m.content}</div>
                  </div>
                ),
              )}
              {chat.streaming !== null && (
                <div className={styles.assistantRow}>
                  <Orb
                    size={28}
                    thinking
                    appearance={{ ...(orb?.appearance ?? DEFAULT_APPEARANCE), floating: false, glow: false }}
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
  )
}
