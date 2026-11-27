import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { mockApi, renderPage } from '@/test/render'
import { NotificationsCard } from './NotificationsCard'

describe('NotificationsCard', () => {
  it('saves quiet hours', async () => {
    const save = vi.fn<(init?: RequestInit) => unknown>(() => ({
      quietFrom: '22:00',
      quietUntil: '07:00',
      timeZone: 'UTC',
      devices: 1,
    }))
    mockApi({
      'GET /api/account/notifications': { timeZone: 'UTC', devices: 1 },
      'PUT /api/account/notifications': save,
    })
    const user = userEvent.setup()
    renderPage(<NotificationsCard />)

    expect(await screen.findByText(/1 device gets them/)).toBeInTheDocument()
    await user.type(screen.getByLabelText('From'), '22:00')
    await user.type(screen.getByLabelText('Until'), '07:00')
    await user.clear(screen.getByLabelText('Time zone'))
    await user.type(screen.getByLabelText('Time zone'), 'Europe/Amsterdam')
    await user.click(screen.getByRole('button', { name: 'Save quiet hours' }))

    expect(JSON.parse(String(save.mock.calls[0]?.[0]?.body))).toEqual({
      quietFrom: '22:00',
      quietUntil: '07:00',
      timeZone: 'Europe/Amsterdam',
    })
  })
})
