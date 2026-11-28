import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DEFAULT_APPEARANCE, type Orb } from '@/api/types'
import { mockApi, renderPage } from '@/test/render'
import { MemoryTab } from './MemoryTab'

const pip: Orb = {
  id: 'o1',
  name: 'Pip',
  description: '',
  instructions: '',
  appearance: DEFAULT_APPEARANCE,
  contextSourceIds: [],
  createdAt: '',
  updatedAt: '',
}

describe('MemoryTab', () => {
  it('lists memories and forgets one', async () => {
    const forget = vi.fn<() => unknown>(() => ({}))
    mockApi({
      'GET /api/orbs/o1/memories': [
        {
          id: 'm1',
          orbId: 'o1',
          content: 'Sam likes tea',
          source: 'learned',
          createdAt: '',
          updatedAt: '',
        },
      ],
      'DELETE /api/memories/m1': forget,
    })
    renderPage(<MemoryTab orb={pip} />)

    expect(await screen.findByText('Sam likes tea')).toBeInTheDocument()
    expect(screen.getByText(/learned in a chat/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Forget "Sam likes tea"' }))

    expect(forget).toHaveBeenCalled()
  })

  it('adds a memory', async () => {
    const add = vi.fn<(init?: RequestInit) => unknown>(() => ({ id: 'm2' }))
    mockApi({ 'GET /api/orbs/o1/memories': [], 'POST /api/orbs/o1/memories': add })
    renderPage(<MemoryTab orb={pip} />)

    await userEvent.type(await screen.findByLabelText('New memory'), 'Sam has a dog')
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

    expect(JSON.parse(String(add.mock.calls[0]?.[0]?.body))).toEqual({ content: 'Sam has a dog' })
  })
})
