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
      isEnabled: true,
    })
  })

  it('edits a connected app and keeps the saved secret when left empty', async () => {
    const update = vi.fn<(init?: RequestInit) => unknown>(() => ({ id: 'i1' }))
    mockApi({
      'GET /api/integrations': [
        {
          id: 'i1',
          kind: 'home-assistant',
          name: 'Home',
          settings: { url: 'http://ha.local:8123' },
          hasSecret: true,
          isEnabled: true,
          tools: [],
          createdAt: '',
        },
      ],
      'GET /api/integrations/kinds': [
        {
          kind: 'home-assistant',
          displayName: 'Home Assistant',
          description: 'Smart home',
          fields: [
            { key: 'url', label: 'Home Assistant address', required: true, secret: false },
            { key: 'token', label: 'Long-lived access token', required: true, secret: true },
          ],
        },
      ],
      'PUT /api/integrations/i1': update,
    })
    renderPage(<IntegrationsCard onChange={() => {}} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))
    expect(screen.getByLabelText('Home Assistant address')).toHaveValue('http://ha.local:8123')
    expect(screen.getByText('Saved. Leave empty to keep it, or type a new one.')).toBeInTheDocument()
    await userEvent.clear(screen.getByLabelText('Name'))
    await userEvent.type(screen.getByLabelText('Name'), 'House')
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await vi.waitFor(() => expect(update).toHaveBeenCalled())
    const body = JSON.parse(String(update.mock.calls[0]?.[0]?.body)) as Record<string, unknown>
    expect(body).toMatchObject({ name: 'House', settings: { url: 'http://ha.local:8123' } })
    expect(body.secret).toBeUndefined()
  })
})
