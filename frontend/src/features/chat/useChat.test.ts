import { reduceChatEvent, type ChatState } from './useChat'

const empty: ChatState = { messages: [], streaming: null, model: null, error: null }
const now = new Date('2026-01-01T09:00:00Z')

describe('reduceChatEvent', () => {
  it('builds a reply from deltas and stores it when done', () => {
    let state = reduceChatEvent(empty, { type: 'start', model: 'echo-1' })
    state = reduceChatEvent(state, { type: 'delta', text: 'Hello' })
    state = reduceChatEvent(state, { type: 'delta', text: ' there' })
    expect(state.streaming).toBe('Hello there')
    expect(state.model).toBe('echo-1')

    state = reduceChatEvent(state, { type: 'done', messageId: 'm1' }, now)
    expect(state.streaming).toBeNull()
    expect(state.messages).toEqual([
      { id: 'm1', role: 'assistant', content: 'Hello there', createdAt: now.toISOString() },
    ])
  })

  it('stops streaming and keeps the error', () => {
    let state = reduceChatEvent(empty, { type: 'start', model: 'm' })
    state = reduceChatEvent(state, { type: 'error', text: 'provider down' })
    expect(state.streaming).toBeNull()
    expect(state.error).toBe('provider down')
    expect(state.messages).toHaveLength(0)
  })

  it('clears an old error when a new reply starts', () => {
    const state = reduceChatEvent({ ...empty, error: 'old' }, { type: 'start', model: 'm' })
    expect(state.error).toBeNull()
  })
})
