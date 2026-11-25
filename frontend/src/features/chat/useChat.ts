import { useCallback, useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { conversationsApi } from '@/api/endpoints'
import { keys } from '@/api/queries'
import type { ChatEvent, Message } from '@/api/types'

export interface ChatState {
  messages: Message[]
  /** The reply as it streams in; null when the orb is not answering. */
  streaming: string | null
  model: string | null
  error: string | null
}

/**
 * Applies one streamed event to the chat state. Pure, so it is easy to test.
 * A finished reply moves from `streaming` into `messages`.
 */
export function reduceChatEvent(state: ChatState, event: ChatEvent, now = new Date()): ChatState {
  switch (event.type) {
    case 'start':
      return { ...state, streaming: '', model: event.model, error: null }
    case 'delta':
      return { ...state, streaming: (state.streaming ?? '') + event.text }
    case 'done':
      return {
        ...state,
        streaming: null,
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
      return { ...state, streaming: null, error: event.text }
  }
}

/** Sends messages to one conversation and streams the orb's replies. */
export function useChat(conversationId: string | undefined, initial: Message[] | undefined) {
  const client = useQueryClient()
  const [state, setState] = useState<ChatState>({ messages: [], streaming: null, model: null, error: null })
  const abort = useRef<AbortController | null>(null)

  // Load server messages when the conversation changes, but never over a reply that is still streaming
  // (starting a new chat navigates to its id while the first reply is in flight).
  useEffect(() => {
    if (abort.current) return
    setState({ messages: initial ?? [], streaming: null, model: null, error: null })
  }, [initial])

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

  return { ...state, isStreaming: state.streaming !== null, send, stop }
}
