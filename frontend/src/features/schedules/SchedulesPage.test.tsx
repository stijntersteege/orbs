import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DEFAULT_APPEARANCE } from '@/api/types'
import { mockApi, renderPage } from '@/test/render'
import { SchedulesPage } from './SchedulesPage'

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

describe('SchedulesPage', () => {
  it('creates a webhook schedule and shows its secret address once', async () => {
    const create = vi.fn<(init?: RequestInit) => unknown>(() => ({
      id: 's1',
      orbId: 'o1',
      name: 'Doorbell',
      prompt: 'Who rang?',
      trigger: 'webhook',
      timeZone: 'UTC',
      isEnabled: true,
      createdAt: '',
      webhookPath: '/api/hooks/s1/secret123',
    }))
    mockApi({
      'GET /api/schedules': [],
      'GET /api/orbs': [pip],
      'GET /api/integrations': [],
      'POST /api/schedules': create,
    })
    const user = userEvent.setup()
    renderPage(<SchedulesPage />)

    await user.type(await screen.findByLabelText('Name'), 'Doorbell')
    await user.type(screen.getByLabelText('What should the orb do?'), 'Who rang?')
    await user.selectOptions(screen.getByLabelText('When'), 'webhook')
    await user.click(screen.getByRole('button', { name: 'Create schedule' }))

    expect(await screen.findByText(/\/api\/hooks\/s1\/secret123/)).toBeInTheDocument()
    expect(JSON.parse(String(create.mock.calls[0]?.[0]?.body))).toMatchObject({
      orbId: 'o1',
      trigger: 'webhook',
    })
  })
})
