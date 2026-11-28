import { activityFromHistory, reduceChatEvent, type ChatState } from './useChat'

const empty: ChatState = {
  messages: [],
  streaming: null,
  model: null,
  error: null,
  activity: [],
  hasMore: false,
  approval: null,
}
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
        tools: [],
      },
    ])
  })

  it('records each tool call and its result with the reply', () => {
    let state = reduceChatEvent(empty, { type: 'start', model: 'm' })
    state = reduceChatEvent(state, { type: 'tool_call', callId: 'c1', tool: 'calculator', arguments: '{}' })
    expect(state.activity[0]!.tools).toEqual([{ callId: 'c1', tool: 'calculator', arguments: '{}' }])

    state = reduceChatEvent(state, {
      type: 'tool_result',
      callId: 'c1',
      tool: 'calculator',
      text: '42',
      isError: false,
      durationMs: 3,
    })
    expect(state.activity[0]!.tools[0]).toMatchObject({ result: '42', isError: false, durationMs: 3 })
  })

  it('rebuilds activity for earlier replies from their saved model and tools', () => {
    const entries = activityFromHistory([
      { id: 'u', role: 'user', content: 'hi', createdAt: '2026-01-01T09:00:00Z' },
      {
        id: 'a',
        role: 'assistant',
        content: 'calculator said: 4',
        createdAt: '2026-01-01T09:00:01Z',
        model: 'echo-1',
        toolCalls: [{ tool: 'calculator', arguments: '{}', result: '4', isError: false, durationMs: 1 }],
      },
    ])
    expect(entries).toHaveLength(1)
    expect(entries[0]).toMatchObject({
      id: 'a',
      model: 'echo-1',
      tools: [{ tool: 'calculator', result: '4' }],
    })
  })

  it('pauses for approval and continues in the same activity entry', () => {
    let state = reduceChatEvent(empty, { type: 'start', model: 'm' })
    state = reduceChatEvent(state, {
      type: 'approval_required',
      approvalId: 'a1',
      callId: 'c1',
      tool: 'send_email',
      arguments: '{}',
    })
    expect(state.streaming).toBeNull()
    expect(state.approval).toMatchObject({ id: 'a1', tool: 'send_email' })

    state = reduceChatEvent(state, { type: 'start', model: 'm', resumed: true })
    expect(state.activity).toHaveLength(1)
    expect(state.approval).toBeNull()
    expect(state.streaming).toBe('')
  })

  it('remembers which orb wrote each reply in a group chat', () => {
    let state = reduceChatEvent(empty, { type: 'start', model: 'm', orbId: 'nova', orbName: 'Nova' })
    state = reduceChatEvent(state, { type: 'delta', text: 'Hi' })
    state = reduceChatEvent(state, { type: 'done', messageId: 'a1' })
    state = reduceChatEvent(state, { type: 'start', model: 'm', orbId: 'pip', orbName: 'Pip' })
    state = reduceChatEvent(state, { type: 'delta', text: 'Hello' })
    state = reduceChatEvent(state, { type: 'done', messageId: 'a2' })

    expect(state.messages.map((m) => [m.content, m.orbId])).toEqual([
      ['Hi', 'nova'],
      ['Hello', 'pip'],
    ])
  })

  it('marks the activity of a failed reply', () => {
    let state = reduceChatEvent(empty, { type: 'start', model: 'm' })
    state = reduceChatEvent(state, { type: 'error', text: 'boom' })
    expect(state.activity[0]).toMatchObject({ failed: true, sources: [] })
  })
})
