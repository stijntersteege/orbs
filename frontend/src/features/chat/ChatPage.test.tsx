import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { Orb, Provider } from '@/api/types'
import { DEFAULT_APPEARANCE } from '@/api/types'
import { mockApi, renderRoute } from '@/test/render'
import { ChatPage } from './ChatPage'

const pip: Orb = {
  id: 'o1',
  name: 'Pip',
  description: 'Work sidekick',
  instructions: '',
  appearance: DEFAULT_APPEARANCE,
  contextSourceIds: [],
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

const echo: Provider = {
  id: 'p1',
  name: 'Echo',
  kind: 'echo',
  hasApiKey: false,
  defaultModel: 'echo-1',
  isEnabled: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

const routes = [
  { path: '/', element: <ChatPage /> },
  { path: '/orbs/:orbId/chat', element: <ChatPage /> },
  { path: '/orbs/:orbId/chat/:conversationId', element: <ChatPage /> },
]

describe('ChatPage', () => {
  it('opens on a new chat with the model picker on top', async () => {
    mockApi({
      'GET /api/orbs': [pip],
      'GET /api/providers': [echo],
      'GET /api/context-sources': [],
      'GET /api/providers/p1/models': [{ id: 'echo-1' }, { id: 'echo-2' }],
    })

    renderRoute(routes, '/')

    expect(await screen.findByRole('heading', { name: 'What can Pip help with?' })).toBeInTheDocument()
    expect(screen.getByLabelText('Message Pip')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Model: Auto' })).toBeInTheDocument()
  })

  it('switches the orb to the model people pick', async () => {
    const save = vi.fn<(init?: RequestInit) => Orb>((_init) => ({
      ...pip,
      model: { providerId: 'p1', model: 'echo-2' },
    }))
    mockApi({
      'GET /api/orbs': [pip],
      'GET /api/providers': [echo],
      'GET /api/context-sources': [],
      'GET /api/providers/p1/models': [{ id: 'echo-1' }, { id: 'echo-2' }],
      'PUT /api/orbs/o1': save,
    })

    renderRoute(routes, '/orbs/o1/chat')

    await userEvent.click(await screen.findByRole('button', { name: 'Model: Auto' }))
    await userEvent.click(await screen.findByRole('menuitemradio', { name: /echo-2/ }))

    expect(save).toHaveBeenCalled()
    const body = JSON.parse(String(save.mock.calls[0]?.[0]?.body)) as { model: unknown }
    expect(body.model).toEqual({ providerId: 'p1', model: 'echo-2' })
  })

  it('asks people to connect a model first', async () => {
    mockApi({ 'GET /api/orbs': [pip], 'GET /api/providers': [], 'GET /api/context-sources': [] })

    renderRoute(routes, '/')

    const links = await screen.findAllByRole('link', { name: 'Connect a model' })
    expect(links[0]).toHaveAttribute('href', '/models')
  })

  it('shows an activity panel with what the orb can read', async () => {
    mockApi({
      'GET /api/orbs': [{ ...pip, contextSourceIds: ['s1'] }],
      'GET /api/providers': [echo],
      'GET /api/context-sources': [
        {
          id: 's1',
          name: 'Roadmap',
          kind: 'text',
          status: 'ready',
          location: '',
          input: '',
          contentLength: 10,
        },
      ],
      'GET /api/conversations': [],
      'GET /api/providers/p1/models': [{ id: 'echo-1' }],
    })

    renderRoute(routes, '/orbs/o1/chat')

    // On narrow screens (jsdom is 1024 px wide) the panel opens from the header.
    await userEvent.click(await screen.findByRole('button', { name: 'Activity', expanded: false }))
    const panel = await screen.findByRole('complementary', { name: 'Activity' })
    expect(panel).toHaveTextContent('When Pip answers, you see here what it read')
    expect(await within(panel).findByText('Roadmap')).toBeInTheDocument()
  })

  it('loads earlier messages of a long chat on request', async () => {
    const message = (id: string, content: string, minute: number) => ({
      id,
      role: 'user' as const,
      content,
      createdAt: `2026-01-01T00:0${minute}:00Z`,
    })
    mockApi({
      'GET /api/orbs': [pip],
      'GET /api/providers': [echo],
      'GET /api/context-sources': [],
      'GET /api/conversations': [],
      'GET /api/providers/p1/models': [{ id: 'echo-1' }],
      'GET /api/conversations/c1': {
        id: 'c1',
        orbId: 'o1',
        title: 'Long chat',
        createdAt: '',
        updatedAt: '',
        messages: [message('m2', 'Second', 2)],
        hasMoreMessages: true,
      },
      'GET /api/conversations/c1/messages': { messages: [message('m1', 'First', 1)], hasMore: false },
    })

    renderRoute(routes, '/orbs/o1/chat/c1')
    expect(await screen.findByText('Second')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Load earlier messages' }))

    expect(await screen.findByText('First')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Load earlier messages' })).not.toBeInTheDocument()
  })
})
