import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { mockApi, renderRoute } from '@/test/render'
import { ContextSourcePage } from './ContextSourcePage'

const base = {
  location: '',
  input: '',
  status: 'ready',
  contentLength: 20,
  createdAt: '',
  updatedAt: '2026-01-01T00:00:00Z',
}
const routes = [
  { path: '/context/:sourceId', element: <ContextSourcePage /> },
  { path: '/context', element: <p>all context</p> },
]

describe('ContextSourcePage', () => {
  it('shows a Markdown file formatted and saves edits back into the file', async () => {
    const replace = vi.fn<(init?: RequestInit) => unknown>(() => ({}))
    mockApi({
      'GET /api/context-sources': [{ ...base, id: 'f1', name: 'Plan', kind: 'file', location: 'plan.md' }],
      'GET /api/orbs': [],
      'GET /api/context-sources/f1/content': {
        id: 'f1',
        kind: 'file',
        content: '# Plan\n\nShip on **Friday**.',
        truncated: false,
        editing: 'textFile',
        fileName: 'plan.md',
        contentType: 'text/markdown',
        size: 24,
      },
      'PUT /api/context-sources/f1': { ...base, id: 'f1', name: 'Plan', kind: 'file' },
      'PUT /api/context-sources/f1/file': replace,
    })
    const user = userEvent.setup()
    renderRoute(routes, '/context/f1')

    expect(await screen.findByRole('heading', { name: 'Plan', level: 1 })).toBeInTheDocument()
    expect(await screen.findByText('Friday', { selector: 'strong' }, { timeout: 5000 })).toBeInTheDocument()
    expect(screen.getByText(/plan\.md · 24 B/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /Edit/ }))
    const editor = screen.getByLabelText('Contents of plan.md')
    await user.clear(editor)
    await user.type(editor, 'Ship on Monday.')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await vi.waitFor(() => expect(replace).toHaveBeenCalled())
    expect(JSON.parse(String(replace.mock.calls[0]![0]?.body))).toEqual({ text: 'Ship on Monday.' })
  })

  it('shows the text orbs read from a PDF and offers the download', async () => {
    mockApi({
      'GET /api/context-sources': [
        { ...base, id: 'p1', name: 'Handbook', kind: 'file', location: 'handbook.pdf' },
      ],
      'GET /api/orbs': [{ id: 'o1', name: 'Pip', contextSourceIds: ['p1'] }],
      'GET /api/context-sources/p1/content': {
        id: 'p1',
        kind: 'file',
        content: 'Welcome to the team.',
        truncated: false,
        editing: 'nameOnly',
        fileName: 'handbook.pdf',
        contentType: 'application/pdf',
        size: 2048,
      },
    })
    renderRoute(routes, '/context/p1')

    expect(await screen.findByText('Welcome to the team.')).toBeInTheDocument()
    expect(screen.getByText(/This is the text Orbs read from the file/)).toBeInTheDocument()
    expect(screen.getByText(/Used by Pip/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Download' })).toBeInTheDocument()
  })
})
