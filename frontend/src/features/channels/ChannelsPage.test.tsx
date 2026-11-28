import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DEFAULT_APPEARANCE } from '@/api/types'
import { mockApi, renderPage } from '@/test/render'
import { ChannelsPage } from './ChannelsPage'

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

describe('ChannelsPage', () => {
  it('connects a Telegram bot and shows how to pair', async () => {
    const created = {
      id: 'c1',
      orbId: 'o1',
      kind: 'telegram',
      address: '@pip_bot',
      settings: {},
      isEnabled: true,
      chats: [],
      createdAt: '',
      pairingCode: 'K7MPQ2',
    }
    let list: unknown[] = []
    const create = vi.fn<(init?: RequestInit) => unknown>(() => {
      list = [created]
      return created
    })
    mockApi({
      'GET /api/channels': () => list,
      'GET /api/orbs': [pip],
      'GET /api/channels/kinds': [
        {
          kind: 'telegram',
          displayName: 'Telegram',
          description: 'A bot',
          fields: [{ key: 'token', label: 'Bot token', required: true, secret: true }],
        },
      ],
      'POST /api/channels': create,
    })
    const user = userEvent.setup()
    renderPage(<ChannelsPage />)

    await user.type(await screen.findByLabelText('Bot token'), '123:abc')
    await user.click(screen.getByRole('button', { name: 'Connect' }))

    expect(await screen.findByText('/start K7MPQ2')).toBeInTheDocument()
    expect(JSON.parse(String(create.mock.calls[0]?.[0]?.body))).toEqual({
      orbId: 'o1',
      kind: 'telegram',
      settings: {},
      secret: '123:abc',
    })
  })
})
