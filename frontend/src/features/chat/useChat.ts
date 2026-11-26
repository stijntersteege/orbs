import { useCallback, useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { conversationsApi } from '@/api/endpoints'
import { keys } from '@/api/queries'
import type { ChatEvent, ChatSource, Message } from '@/api/types'

/** What the orb did for one reply: which model and provider answered, what it read and how long it took. */
export interface ActivityEntry {
  id: string
  model: string
  provider?: string
  sources: ChatSource[]
  startedAt: number
  /** Set when the reply is complete. */
  finishedAt?: number
  failed?: boolean
}

export interface ChatState {
  messages: Message[]
  /** The reply as it streams in; null when the orb is not answering. */
  streaming: string | null
  model: string | null
  error: string | null
  /** Replies in this session, newest last. The server does not store activity, so older chats start empty. */
  activity: ActivityEntry[]
}

const current = (state: ChatState) => state.activity.at(-1)

function finishActivity(state: ChatState, patch: Partial<ActivityEntry>, now: number): ActivityEntry[] {
  const last = current(state)
  if (!last || last.finishedAt) return state.activity
  return [...state.activity.slice(0, -1), { ...last, finishedAt: now, ...patch }]
}

/**
 * Applies one streamed event to the chat state. Pure, so it is easy to test.
 * A finished reply moves from `streaming` into `messages`.
 */
export function reduceChatEvent(state: ChatState, event: ChatEvent, now = new Date()): ChatState {
  switch (event.type) {
    case 'start':
      return {
        ...state,
        streaming: '',
        model: event.model,
        error: null,
        activity: [
          ...state.activity,
          {
            id: `reply-${state.activity.length + 1}`,
            model: event.model,
            provider: event.provider,
            sources: event.sources ?? [],
            startedAt: now.getTime(),
          },
        ],
      }
    case 'delta':
      return { ...state, streaming: (state.streaming ?? '') + event.text }
    case 'done':
      return {
        ...state,
        streaming: null,
        activity: finishActivity(state, { id: event.messageId }, now.getTime()),
        messages: [
          ...state.messages,
          {
            id: event.messageId,
            role: 'assistant',
            content: state.streaming ?? '',
            createdAt: now.toISOString(),
          },
        ],
      }
    case 'error':
      return {
        ...state,
        streaming: null,
        error: event.text,
        activity: finishActivity(state, { failed: true }, now.getTime()),
      }
  }
}

/** Sends messages to one conversation and streams the orb's replies. */
export function useChat(conversationId: string | undefined, initial: Message[] | undefined) {
  const client = useQueryClient()
  const [state, setState] = useState<ChatState>({
    messages: [],
    streaming: null,
    model: null,
    error: null,
    activity: [],
  })
  const abort = useRef<AbortController | null>(null)
  // The conversation whose activity is on screen. Refetching it keeps the activity; another chat starts empty.
  const activityFor = useRef<string | undefined>(conversationId)

  // Load server messages when the conversation changes, but never over a reply that is still streaming
  // (starting a new chat navigates to its id while the first reply is in flight).
  useEffect(() => {
    if (abort.current) return
    const same = activityFor.current === conversationId
    activityFor.current = conversationId
    setState((s) => ({
      messages: initial ?? [],
      streaming: null,
      model: null,
      error: null,
      activity: same ? s.activity : [],
    }))
  }, [initial, conversationId])

  useEffect(() => () => abort.current?.abort(), [])

  const send = useCallback(
    async (content: string, targetId = conversationId) => {
      if (!targetId || !content.trim()) return
      const controller = new AbortController()
      abort.current = controller
      const userMessage: Message = {
        id: `local-${Date.now()}`,
        role: 'user',
        content,
        createdAt: new Date().toISOString(),
      }
      setState((s) => ({ ...s, messages: [...s.messages, userMessage], streaming: '', error: null }))
      try {
        for await (const event of conversationsApi.send(targetId, content, controller.signal)) {
          setState((s) => reduceChatEvent(s, event))
        }
      } catch (error) {
        if (!controller.signal.aborted) {
          setState((s) => ({
            ...s,
            streaming: null,
            error: error instanceof Error ? error.message : 'Sending failed.',
          }))
        } else {
          setState((s) => ({ ...s, streaming: null }))
        }
      } finally {
        abort.current = null
        void client.invalidateQueries({ queryKey: ['conversations'] })
        void client.invalidateQueries({ queryKey: keys.conversation(targetId) })
      }
    },
    [client, conversationId],
  )

  const stop = useCallback(() => abort.current?.abort(), [])

  /** Call when a new conversation is created for the first message, so its activity is kept. */
  const adopt = useCallback((id: string) => {
    activityFor.current = id
  }, [])

  return { ...state, isStreaming: state.streaming !== null, send, stop, adopt }
}
