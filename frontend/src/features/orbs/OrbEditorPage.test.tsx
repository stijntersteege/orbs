import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DEFAULT_APPEARANCE, type SaveOrbRequest } from '@/api/types'
import { mockApi, renderRoute } from '@/test/render'
import { OrbEditorPage } from './OrbEditorPage'

const routes = [
  { path: '/orbs/new', element: <OrbEditorPage /> },
  { path: '/orbs/:orbId/chat', element: <p>chat page</p> },
]

describe('OrbEditorPage', () => {
  it('creates an orb with its name, context and look', async () => {
    const user = userEvent.setup()
    let sent: SaveOrbRequest | undefined
    mockApi({
      'GET /api/providers': [],
      'GET /api/context-sources': [
        {
          id: 's1',
          name: 'Roadmap',
          kind: 'text',
          location: '',
          input: 'x',
          status: 'ready',
          contentLength: 1,
        },
      ],
      'POST /api/orbs': (init?: RequestInit) => {
        sent = JSON.parse(String(init?.body)) as SaveOrbRequest
        return { ...sent, id: 'new-id', appearance: sent.appearance ?? DEFAULT_APPEARANCE }
      },
    })

    const { router } = renderRoute(routes, '/orbs/new')

    await user.type(screen.getByLabelText('Name'), 'Byte')
    await user.click(await screen.findByRole('checkbox', { name: /Roadmap/ }))
    await user.click(screen.getByRole('button', { name: 'Color: Mint' }))
    await user.click(screen.getByRole('button', { name: 'Create Byte' }))

    expect(await screen.findByText('chat page')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/orbs/new-id/chat')
    expect(sent).toMatchObject({ name: 'Byte', contextSourceIds: ['s1'], model: null })
    expect(sent?.appearance?.color).toBe('mint')
  })

  it('cannot be submitted without a name', async () => {
    mockApi({ 'GET /api/providers': [], 'GET /api/context-sources': [] })

    renderRoute(routes, '/orbs/new')

    expect(await screen.findByRole('button', { name: 'Create orb' })).toBeDisabled()
    expect(screen.getByText('Give it a name to continue.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'What your orb knows' })).toBeInTheDocument()
  })

  it('saves only when something changed, and can undo changes', async () => {
    const user = userEvent.setup()
    const pip = {
      id: 'o1',
      name: 'Pip',
      description: '',
      instructions: '',
      appearance: DEFAULT_APPEARANCE,
      contextSourceIds: [],
      createdAt: '',
      updatedAt: '',
    }
    mockApi({
      'GET /api/orbs/o1': pip,
      'GET /api/providers': [],
      'GET /api/context-sources': [],
      'GET /api/tools': [],
    })
    renderRoute([{ path: '/orbs/:orbId', element: <OrbEditorPage /> }], '/orbs/o1')

    const save = await screen.findByRole('button', { name: 'Save changes' })
    expect(save).toBeDisabled()
    await user.type(screen.getByLabelText('Description'), 'Helper')
    expect(save).toBeEnabled()
    expect(screen.getByText('Unsaved changes')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Undo changes' }))
    expect(screen.getByLabelText('Description')).toHaveValue('')
    expect(save).toBeDisabled()
  })

  it('asks before leaving with unsaved changes', async () => {
    const user = userEvent.setup()
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    mockApi({ 'GET /api/providers': [], 'GET /api/context-sources': [], 'GET /api/tools': [] })
    const { router } = renderRoute(routes, '/orbs/new')

    await user.type(screen.getByLabelText('Name'), 'Byte')
    void router.navigate('/orbs/x/chat')

    await vi.waitFor(() => expect(confirm).toHaveBeenCalled())
    expect(router.state.location.pathname).toBe('/orbs/new')
    confirm.mockRestore()
  })
})
