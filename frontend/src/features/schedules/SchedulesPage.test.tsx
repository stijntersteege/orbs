import { screen, within } from '@testing-library/react'
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

  it('pauses a schedule and edits its prompt', async () => {
    const schedule = {
      id: 's1',
      orbId: 'o1',
      name: 'Weekly',
      prompt: 'Sum up my week',
      trigger: 'time',
      cron: '0 9 * * 1',
      timeZone: 'UTC',
      isEnabled: true,
      createdAt: '',
    }
    const update = vi.fn<(init?: RequestInit) => unknown>((init) => ({
      ...schedule,
      ...(JSON.parse(String(init?.body)) as object),
    }))
    mockApi({
      'GET /api/schedules': [schedule],
      'GET /api/orbs': [pip],
      'GET /api/integrations': [],
      'PUT /api/schedules/s1': update,
    })
    const user = userEvent.setup()
    renderPage(<SchedulesPage />)

    await user.click(await screen.findByRole('button', { name: 'Pause' }))
    await vi.waitFor(() => expect(update).toHaveBeenCalledTimes(1))
    expect(JSON.parse(String(update.mock.calls[0]![0]?.body))).toMatchObject({
      isEnabled: false,
      prompt: 'Sum up my week',
    })

    await user.click(screen.getByRole('button', { name: 'Edit' }))
    const form = within(screen.getByRole('form', { name: 'Edit Weekly' }))
    const prompt = form.getByLabelText('What should the orb do?')
    await user.clear(prompt)
    await user.type(prompt, 'Plan my week')
    await user.click(form.getByRole('button', { name: 'Save changes' }))

    await vi.waitFor(() => expect(update).toHaveBeenCalledTimes(2))
    expect(JSON.parse(String(update.mock.calls[1]![0]?.body))).toMatchObject({
      prompt: 'Plan my week',
      cron: '0 9 * * 1',
    })
  })
})
