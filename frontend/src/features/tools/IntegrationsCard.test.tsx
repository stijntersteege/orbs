import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { mockApi, renderPage } from '@/test/render'
import { IntegrationsCard } from './IntegrationsCard'

describe('IntegrationsCard', () => {
  it('builds the form from the app kind and sends the secret separately', async () => {
    const create = vi.fn<(init?: RequestInit) => unknown>(() => ({ id: 'i1' }))
    mockApi({
      'GET /api/integrations': [],
      'GET /api/integrations/kinds': [
        {
          kind: 'home-assistant',
          displayName: 'Home Assistant',
          description: 'Smart home',
          fields: [
            { key: 'url', label: 'Home Assistant address', required: true, secret: false },
            { key: 'token', label: 'Long-lived access token', required: false, secret: true },
          ],
        },
      ],
      'POST /api/integrations': create,
    })
    renderPage(<IntegrationsCard onChange={() => {}} />)

    await userEvent.click(await screen.findByRole('button', { name: '+ Connect an app' }))
    await userEvent.type(await screen.findByLabelText('Home Assistant address'), 'http://ha.local:8123')
    await userEvent.type(screen.getByLabelText('Long-lived access token'), 'abc')
    await userEvent.click(screen.getByRole('button', { name: 'Connect Home Assistant' }))

    const body = JSON.parse(String(create.mock.calls[0]?.[0]?.body)) as Record<string, unknown>
    expect(body).toEqual({
      kind: 'home-assistant',
      name: 'Home Assistant',
      settings: { url: 'http://ha.local:8123' },
      secret: 'abc',
    })
  })
})
