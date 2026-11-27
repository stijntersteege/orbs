import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { conversationsApi } from '@/api/endpoints'
import {
  keys,
  useContextSources,
  useConversation,
  useConversations,
  useVoiceStatus,
  useOrbs,
  useProviders,
  useSaveOrb,
} from '@/api/queries'
import { DEFAULT_APPEARANCE, type ChatSource, type ModelRef, type Orb as OrbModel } from '@/api/types'
import { Orb, type OrbState } from '@/components/orb/Orb'
import { Button, ErrorNotice, Spinner } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import { useSettings } from '@/app/SettingsContext'
import { ApprovalCard } from './ApprovalCard'
import { useChat } from './useChat'
import { recordingSupported, useVoice } from './useVoice'
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
  const chat = useChat(
    conversationId,
    conversation.data?.messages,
    conversation.data?.hasMoreMessages,
    conversation.data?.pendingApproval ?? null,
  )
  const [draft, setDraft] = useState('')
  const [pendingModel, setPendingModel] = useState<ModelRef | undefined>()
  const [error, setError] = useState<unknown>(null)
  const [activityOpen, setActivityOpen] = useState(false)
  const endRef = useRef<HTMLDivElement>(null)
  const voiceStatus = useVoiceStatus()
  const formRef = useRef<HTMLFormElement>(null)
  // What was said goes into the message box and is sent right away.
  const voice = useVoice((text) => {
    setDraft(text)
    requestAnimationFrame(() => formRef.current?.requestSubmit())
  })
  const spokenUpTo = useRef<string | undefined>(undefined)
  const inputRef = useRef<HTMLTextAreaElement>(null)

  // A new chat starts with the orb you used last.
  useEffect(() => {
    if (orb && orb.id !== settings.lastOrbId) update({ lastOrbId: orb.id })
  }, [orb, settings.lastOrbId, update])

  // Keep the newest text in view whenever a message arrives or the reply grows.
  // Keyed on the newest message, so loading earlier messages does not jump to the end.
  const lastActivity = `${chat.messages.at(-1)?.id ?? ''}:${chat.streaming?.length ?? -1}`
  useEffect(() => {
    if (lastActivity) endRef.current?.scrollIntoView?.({ block: 'end' })
  }, [lastActivity])

  // Read each new reply aloud when voice replies are on. Replies already there when the chat opened stay quiet.
  const lastMessage = chat.messages.at(-1)
  useEffect(() => {
    if (!lastMessage || chat.isStreaming) return
    if (spokenUpTo.current === undefined) {
      spokenUpTo.current = lastMessage.id
      return
    }
    if (lastMessage.id === spokenUpTo.current) return
    spokenUpTo.current = lastMessage.id
    if (lastMessage.role === 'assistant' && settings.voiceReplies && voiceStatus.data?.canSpeak) {
      void voice.speak(lastMessage.content, undefined, orb?.id)
    }
  }, [lastMessage, chat.isStreaming, settings.voiceReplies, voiceStatus.data?.canSpeak, voice, orb?.id])

  if (orbs.isPending || providers.isPending) return <Spinner />
  if (orbs.error) return <ErrorNotice error={orbs.error} />
  if (params.orbId && !orb) return <ErrorNotice error={new Error('Orb not found.')} />

  const allProviders = providers.data ?? []
  const hasModel = allProviders.some((p) => p.isEnabled)
  const known = orb ? (sources.data?.filter((s) => orb.contextSourceIds.includes(s.id)) ?? []) : []
  const model = orb ? orb.model : pendingModel
  const name = orb?.name ?? 'Pip'
  const citations = new Map(chat.activity.map((a) => [a.id, a.sources]))
  const runningTool = chat.activity.at(-1)?.tools.find((t) => t.result === undefined)?.tool
  const orbById = (id?: string) => (id ? orbs.data?.find((o) => o.id === id) : undefined)
  const chatOrbIds = new Set([
    ...(conversation.data?.orbIds ?? []),
    ...chat.messages.flatMap((m) => (m.orbId ? [m.orbId] : [])),
  ])
  const isGroup = chatOrbIds.size > 1
  // "@" at the end of the message box suggests orbs to hand the question to.
  const mentionQuery = /(?:^|\s)@([\w-]*)$/.exec(draft)?.[1]
  const mentionChoices =
    mentionQuery === undefined
      ? []
      : (orbs.data ?? [])
          .filter((o) => o.name.toLowerCase().startsWith(mentionQuery.toLowerCase()))
          .slice(0, 5)
  const canListen = voiceStatus.data?.canListen && recordingSupported()
  // The orb's face shows what it is doing right now.
  const faceState: OrbState =
    voice.state === 'listening' || voice.state === 'transcribing'
      ? 'listening'
      : voice.state === 'speaking'
        ? 'speaking'
        : chat.approval
          ? 'waiting'
          : chat.isStreaming
            ? 'thinking'
            : 'idle'
  const canSpeak = !!voiceStatus.data?.canSpeak
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
    <form ref={formRef} className={styles.composer} onSubmit={submit}>
      {mentionChoices.length > 0 && (
        <div className={styles.mentions}>
          {mentionChoices.map((o) => (
            <button
              key={o.id}
              type="button"
              aria-label={`Mention ${o.name}`}
              className={styles.mention}
              onClick={() => {
                setDraft(draft.replace(/@([\w-]*)$/, `@${o.name} `))
                inputRef.current?.focus()
              }}
            >
              <Orb
                size={18}
                appearance={{ ...o.appearance, glow: false, floating: false, accessory: 'none' }}
              />
              {o.name}
            </button>
          ))}
        </div>
      )}
      <label htmlFor="message" className="visually-hidden">
        Message {name}
      </label>
      <textarea
        id="message"
        ref={inputRef}
        className={styles.input}
        rows={1}
        value={draft}
        placeholder={hasModel ? `Message ${name}, or @mention another orb` : 'Connect a model to start'}
        onChange={(e) => onDraft(e.target)}
        onKeyDown={onKeyDown}
      />
      <div className={styles.composerBar}>
        <span className={styles.composerHint}>
          {orb
            ? `${orb.name} answers with ${chat.model ?? model?.model ?? 'Auto'}`
            : 'Your first orb is made when you send'}
        </span>
        {canSpeak && (
          <button
            type="button"
            className={styles.voiceButton}
            aria-pressed={settings.voiceReplies}
            aria-label="Read replies aloud"
            title="Read replies aloud"
            onClick={() => {
              if (voice.state === 'speaking') voice.stopSpeaking()
              update({ voiceReplies: !settings.voiceReplies })
            }}
          >
            <Icon name="speaker" size={18} />
          </button>
        )}
        {canListen && !chat.isStreaming && (
          <button
            type="button"
            className={styles.voiceButton}
            data-state={voice.state}
            aria-pressed={voice.state === 'listening'}
            aria-label={voice.state === 'listening' ? 'Stop and send' : 'Talk'}
            disabled={voice.state === 'transcribing'}
            onClick={() => (voice.state === 'listening' ? voice.stopListening() : void voice.listen())}
          >
            <Icon name="mic" size={18} />
          </button>
        )}
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
              state={faceState}
              label={faceState === 'idle' ? undefined : `${orb.name} is ${faceState}`}
              appearance={{ ...orb.appearance, glow: false, floating: false, accessory: 'none' }}
            />
          )}
          <h1 className={styles.orbTitle}>{name}</h1>
          {isGroup &&
            [...chatOrbIds]
              .filter((id) => id !== orb?.id)
              .map((id) => orbById(id))
              .filter((o) => !!o)
              .map((o) => (
                <span key={o!.id} title={o!.name} className={styles.guest}>
                  <Orb
                    size={22}
                    appearance={{ ...o!.appearance, glow: false, floating: false, accessory: 'none' }}
                  />
                  <span className="visually-hidden">{o!.name} is in this chat</span>
                </span>
              ))}
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
                {chat.hasMore && (
                  <Button
                    small
                    variant="ghost"
                    className={styles.loadEarlier}
                    disabled={chat.loadingEarlier}
                    onClick={() => void chat.loadEarlier()}
                  >
                    {chat.loadingEarlier ? 'Loading…' : 'Load earlier messages'}
                  </Button>
                )}
                {chat.messages.map((m) => {
                  if (m.role === 'user') {
                    return (
                      <div key={m.id} className={styles.user}>
                        {m.content}
                      </div>
                    )
                  }
                  const author = orbById(m.orbId) ?? orb
                  return (
                    <div key={m.id} className={styles.assistantRow}>
                      <Orb
                        size={28}
                        appearance={{
                          ...(author?.appearance ?? DEFAULT_APPEARANCE),
                          floating: false,
                          glow: false,
                        }}
                      />
                      <div className={styles.assistant}>
                        {isGroup && author && <span className={styles.author}>{author.name}</span>}
                        {m.content}
                        <Cites sources={citations.get(m.id)} />
                      </div>
                    </div>
                  )
                })}
                {chat.streaming !== null && (
                  <div className={styles.assistantRow}>
                    <Orb
                      size={28}
                      state={faceState === 'idle' ? 'thinking' : faceState}
                      appearance={{
                        ...((orbById(chat.replyingOrbId) ?? orb)?.appearance ?? DEFAULT_APPEARANCE),
                        floating: false,
                        glow: false,
                      }}
                    />
                    <div className={styles.assistant} aria-busy="true">
                      {isGroup && orbById(chat.replyingOrbId) && (
                        <span className={styles.author}>{orbById(chat.replyingOrbId)!.name}</span>
                      )}
                      {chat.streaming || (
                        <span className={styles.thinking}>
                          {runningTool ? `Using ${runningTool}…` : 'Thinking…'}
                        </span>
                      )}
                    </div>
                  </div>
                )}
                {chat.approval && (
                  <ApprovalCard
                    orbName={name}
                    approval={chat.approval}
                    onDecide={(d) => void chat.decide(d)}
                  />
                )}
                <div ref={endRef} />
              </div>
            </div>
            <div className={styles.dock}>
              <ErrorNotice
                error={
                  error ?? (chat.error ? new Error(chat.error) : voice.error ? new Error(voice.error) : null)
                }
              />
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
