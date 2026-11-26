import { readServerSentEvents } from '@/api/sse'
import type { ChatEvent, Conversation, Orb } from '@/api/types'
import { installDemo } from './install'
import { DemoServer } from './server'

const base = 'http://demo.test'

async function call<T>(server: DemoServer, method: string, path: string, body?: unknown) {
  const response = await server.handle(method, new URL(path, base), body)
  return { status: response.status, body: (response.status === 204 ? undefined : await response.json()) as T }
}

describe('DemoServer', () => {
  it('starts with sample orbs, a provider and a note', async () => {
    const server = new DemoServer()
    const orbs = await call<Orb[]>(server, 'GET', '/api/orbs')
    expect(orbs.body.map((o) => o.name)).toEqual(['Pip', 'Nova', 'Basil'])
    expect((await call<unknown[]>(server, 'GET', '/api/providers')).body).toHaveLength(1)
  })

  it('creates, updates and deletes an orb and keeps it across reloads', async () => {
    const server = new DemoServer()
    const created = await call<Orb>(server, 'POST', '/api/orbs', { name: 'Zed' })
    expect(created.status).toBe(201)

    await call(server, 'PUT', `/api/orbs/${created.body.id}`, { name: 'Zed 2' })
    const reloaded = new DemoServer()
    expect((await call<Orb>(reloaded, 'GET', `/api/orbs/${created.body.id}`)).body.name).toBe('Zed 2')

    expect((await call(reloaded, 'DELETE', `/api/orbs/${created.body.id}`)).status).toBe(204)
    expect((await call(reloaded, 'GET', `/api/orbs/${created.body.id}`)).status).toBe(404)
  })

  it('rejects an orb without a name as a problem response', async () => {
    const result = await call<{ detail: string }>(new DemoServer(), 'POST', '/api/orbs', { name: ' ' })
    expect(result.status).toBe(400)
    expect(result.body.detail).toBe('The name is required.')
  })

  it('streams an echo reply and stores the conversation', async () => {
    const server = new DemoServer({ wordDelay: 0 })
    const conversation = await call<Conversation>(server, 'POST', '/api/conversations', {
      orbId: 'demo-orb-pip',
    })

    const response = await server.handle(
      'POST',
      new URL(`/api/conversations/${conversation.body.id}/messages`, base),
      {
        content: 'hello there',
      },
    )
    const events: ChatEvent[] = []
    for await (const event of readServerSentEvents<ChatEvent>(response.body!)) events.push(event)

    expect(events[0]).toMatchObject({ type: 'start', model: 'echo-1', provider: 'Echo (demo)' })
    const start = events[0]
    expect(start?.type === 'start' ? start.sources?.map((s) => s.name) : []).toEqual(['About Orbs'])
    const text = events.flatMap((e) => (e.type === 'delta' ? [e.text] : [])).join('')
    expect(text).toBe('You said: hello there')
    expect(events.at(-1)?.type).toBe('done')

    const stored = await call<Conversation>(server, 'GET', `/api/conversations/${conversation.body.id}`)
    expect(stored.body.title).toBe('hello there')
    expect(stored.body.messages.map((m) => m.role)).toEqual(['user', 'assistant'])
  })

  it('never calls real providers and explains why', async () => {
    const server = new DemoServer({ wordDelay: 0 })
    const provider = await call<{ id: string }>(server, 'POST', '/api/providers', {
      name: 'OpenAI',
      kind: 'openai-compatible',
      baseUrl: 'https://api.openai.com/v1',
      apiKey: 'sk-secret',
      defaultModel: 'gpt-x',
    })
    await call(server, 'PUT', '/api/orbs/demo-orb-pip', {
      name: 'Pip',
      model: { providerId: provider.body.id, model: 'gpt-x' },
    })
    const conversation = await call<Conversation>(server, 'POST', '/api/conversations', {
      orbId: 'demo-orb-pip',
    })
    const response = await server.handle(
      'POST',
      new URL(`/api/conversations/${conversation.body.id}/messages`, base),
      {
        content: 'hi',
      },
    )
    const events: ChatEvent[] = []
    for await (const event of readServerSentEvents<ChatEvent>(response.body!)) events.push(event)

    expect(events.map((e) => (e.type === 'delta' ? e.text : '')).join('')).toContain('live demo')
    expect(localStorage.getItem('orbs.demo.v1')).not.toContain('sk-secret')
  })

  it('answers /api requests through fetch and passes others through', async () => {
    const passthrough = vi.fn<() => Promise<Response>>(async () => new Response('ok'))
    vi.stubGlobal('fetch', passthrough)
    installDemo(new DemoServer())

    const orbs = (await (await fetch('/api/orbs')).json()) as Orb[]
    expect(orbs).toHaveLength(3)
    await fetch('/favicon.svg')
    expect(passthrough).toHaveBeenCalledTimes(1)
  })
})
