import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { mockApi, renderPage } from '@/test/render'
import { ToolsPage } from './ToolsPage'

describe('ToolsPage', () => {
  it('lists tools and connects an MCP server', async () => {
    const create = vi.fn<(init?: RequestInit) => unknown>(() => ({ id: 'm1', name: 'Home' }))
    mockApi({
      'GET /api/tools': [
        { name: 'calculator', displayName: 'Calculator', description: 'Sums', risk: 'read' },
        { name: 'home__turn_on', displayName: 'Turn on (Home)', description: 'Lights', risk: 'write' },
      ],
      'GET /api/mcp-servers': [],
      'POST /api/mcp-servers': create,
    })
    renderPage(<ToolsPage />)

    expect(await screen.findByText('Turn on (Home)')).toBeInTheDocument()
    expect(screen.getByText('acts')).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Name'), 'Home')
    await userEvent.type(screen.getByLabelText('Server URL'), 'https://mcp.example.com/mcp')
    await userEvent.click(screen.getByRole('button', { name: 'Connect server' }))

    const body = JSON.parse(String(create.mock.calls[0]?.[0]?.body)) as Record<string, unknown>
    expect(body).toMatchObject({ name: 'Home', transport: 'http', location: 'https://mcp.example.com/mcp' })
  })
})
