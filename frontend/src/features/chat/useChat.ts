import { useCallback, useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { conversationsApi } from '@/api/endpoints'
import { keys } from '@/api/queries'
import type { Approval, ApprovalDecision, ChatEvent, ChatSource, Message, ToolCallRecord } from '@/api/types'

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
  /** Tools used for this reply, in order. A call without a result is still running. */
  tools: ToolActivity[]
}

export interface ToolActivity extends Partial<ToolCallRecord> {
  callId: string
  tool: string
  arguments: string
}

/** Activity for replies loaded from the server: the model that wrote each one and the tools it used. */
export function activityFromHistory(messages: Message[]): ActivityEntry[] {
  return messages
    .filter((m) => m.role === 'assistant' && m.model)
    .map((m) => {
      const at = Date.parse(m.createdAt)
      return {
        id: m.id,
        model: m.model!,
        sources: [],
        startedAt: at,
        finishedAt: at,
        tools: (m.toolCalls ?? []).map((t, i) => ({ ...t, callId: `${m.id}-${i}` })),
      }
    })
}

export interface ChatState {
  messages: Message[]
  /** The reply as it streams in; null when the orb is not answering. */
  streaming: string | null
  model: string | null
  error: string | null
  /** Replies in this session, newest last. The server does not store activity, so older chats start empty. */
  activity: ActivityEntry[]
  /** True while older messages exist on the server that are not loaded yet. */
  hasMore: boolean
  /** A tool call that waits for your answer; the reply continues once you give it. */
  approval: Approval | null
  /** The orb writing the reply that streams right now. */
  replyingOrbId?: string
}

const current = (state: ChatState) => state.activity.at(-1)

function withCurrent(state: ChatState, change: (entry: ActivityEntry) => ActivityEntry): ChatState {
  const last = current(state)
  if (!last) return state
  return { ...state, activity: [...state.activity.slice(0, -1), change(last)] }
}

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
      // A reply that continues after an approval keeps its activity entry.
      if (event.resumed && current(state)) return { ...state, streaming: '', error: null, approval: null }
      return {
        ...state,
        replyingOrbId: event.orbId,
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
            tools: [],
          },
        ],
      }
    case 'approval_required':
      return {
        ...state,
        streaming: null,
        approval: {
          id: event.approvalId,
          callId: event.callId,
          tool: event.tool,
          arguments: event.arguments,
        },
      }
    case 'tool_call':
      return withCurrent(state, (entry) => ({
        ...entry,
        tools: [...entry.tools, { callId: event.callId, tool: event.tool, arguments: event.arguments }],
      }))
    case 'tool_result':
      return withCurrent(state, (entry) => ({
        ...entry,
        tools: entry.tools.map((t) =>
          t.callId === event.callId
            ? {
                ...t,
                result: event.text,
                isError: event.isError,
                durationMs: event.durationMs,
                decision: event.decision,
              }
            : t,
        ),
      }))
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
            orbId: state.replyingOrbId,
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
export function useChat(
  conversationId: string | undefined,
  initial: Message[] | undefined,
  initialHasMore = false,
  initialApproval: Approval | null = null,
) {
  const client = useQueryClient()
  const [state, setState] = useState<ChatState>({
    messages: [],
    streaming: null,
    model: null,
    error: null,
    activity: [],
    hasMore: false,
    approval: null,
  })
  const [loadingEarlier, setLoadingEarlier] = useState(false)
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
      activity: same && s.activity.length > 0 ? s.activity : activityFromHistory(initial ?? []),
      hasMore: initialHasMore,
      approval: initialApproval,
    }))
  }, [initial, conversationId, initialHasMore, initialApproval])

  useEffect(() => () => abort.current?.abort(), [])

  /** Streams events into the state, whatever started them: a message or an answer to an approval. */
  const stream = useCallback(
    async (targetId: string, events: (signal: AbortSignal) => AsyncGenerator<ChatEvent>) => {
      const controller = new AbortController()
      abort.current = controller
      try {
        for await (const event of events(controller.signal)) {
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
        // The orb may have remembered or forgotten something.
        void client.invalidateQueries({ queryKey: ['memories'] })
      }
    },
    [client],
  )

  const send = useCallback(
    async (content: string, targetId = conversationId) => {
      if (!targetId || !content.trim()) return
      const userMessage: Message = {
        id: `local-${Date.now()}`,
        role: 'user',
        content,
        createdAt: new Date().toISOString(),
      }
      setState((s) => ({
        ...s,
        messages: [...s.messages, userMessage],
        streaming: '',
        error: null,
        approval: null,
      }))
      await stream(targetId, (signal) => conversationsApi.send(targetId, content, signal))
    },
    [conversationId, stream],
  )

  /** Answers the waiting tool call; the reply continues in the same bubble. */
  const decide = useCallback(
    async (decision: ApprovalDecision) => {
      const approval = state.approval
      if (!conversationId || !approval) return
      setState((s) => ({ ...s, approval: null, streaming: '', error: null }))
      await stream(conversationId, (signal) =>
        conversationsApi.decide(conversationId, approval.id, decision, signal),
      )
    },
    [conversationId, state.approval, stream],
  )

  const stop = useCallback(() => abort.current?.abort(), [])

  /** Prepends the page of messages before the oldest one on screen. */
  const loadEarlier = useCallback(async () => {
    const oldest = state.messages[0]
    if (!conversationId || !oldest || loadingEarlier) return
    setLoadingEarlier(true)
    try {
      const page = await conversationsApi.messages(conversationId, oldest.createdAt)
      setState((s) => ({ ...s, messages: [...page.messages, ...s.messages], hasMore: page.hasMore }))
    } catch (error) {
      setState((s) => ({ ...s, error: error instanceof Error ? error.message : 'Loading failed.' }))
    } finally {
      setLoadingEarlier(false)
    }
  }, [conversationId, loadingEarlier, state.messages])

  /** Call when a new conversation is created for the first message, so its activity is kept. */
  const adopt = useCallback((id: string) => {
    activityFor.current = id
  }, [])

  return {
    ...state,
    isStreaming: state.streaming !== null,
    send,
    decide,
    stop,
    adopt,
    loadEarlier,
    loadingEarlier,
  }
}
