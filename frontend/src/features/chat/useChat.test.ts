import { reduceChatEvent, type ChatState } from './useChat'

const empty: ChatState = { messages: [], streaming: null, model: null, error: null, activity: [] }
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

  it('records what the orb read and how long the reply took', () => {
    const source = { id: 's1', name: 'Roadmap', kind: 'text', characters: 120, truncated: false }
    const t0 = new Date('2026-01-01T09:00:00Z')
    const t1 = new Date('2026-01-01T09:00:02Z')
    let state = reduceChatEvent(
      empty,
      { type: 'start', model: 'm', provider: 'OpenRouter', sources: [source] },
      t0,
    )
    state = reduceChatEvent(state, { type: 'delta', text: 'Hi' }, t0)
    state = reduceChatEvent(state, { type: 'done', messageId: 'a1' }, t1)

    expect(state.activity).toEqual([
      {
        id: 'a1',
        model: 'm',
        provider: 'OpenRouter',
        sources: [source],
        startedAt: t0.getTime(),
        finishedAt: t1.getTime(),
      },
    ])
  })

  it('marks the activity of a failed reply', () => {
    let state = reduceChatEvent(empty, { type: 'start', model: 'm' })
    state = reduceChatEvent(state, { type: 'error', text: 'boom' })
    expect(state.activity[0]).toMatchObject({ failed: true, sources: [] })
  })
})
