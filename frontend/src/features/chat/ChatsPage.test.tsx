import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { mockApi, renderRoute } from '@/test/render'
import { ChatsPage } from './ChatsPage'

const chat = {
  id: 'c1',
  orbId: 'o1',
  title: 'Old name',
  createdAt: '2026-01-01T09:00:00Z',
  updatedAt: '2026-01-01T09:00:00Z',
}

describe('ChatsPage', () => {
  it('renames a chat', async () => {
    const rename = vi.fn<(init?: RequestInit) => unknown>(() => ({ ...chat, title: 'Trip plans' }))
    mockApi({
      'GET /api/orbs/o1': { id: 'o1', name: 'Pip' },
      'GET /api/conversations': [chat],
      'PUT /api/conversations/c1': rename,
    })
    const user = userEvent.setup()
    renderRoute([{ path: '/orbs/:orbId/chats', element: <ChatsPage /> }], '/orbs/o1/chats')

    expect(await screen.findByRole('heading', { name: 'Chats with Pip' })).toBeInTheDocument()
    await user.click(await screen.findByRole('button', { name: 'Rename' }))
    const input = screen.getByLabelText('Chat name')
    await user.clear(input)
    await user.type(input, 'Trip plans')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await vi.waitFor(() => expect(rename).toHaveBeenCalled())
    expect(JSON.parse(String(rename.mock.calls[0]![0]?.body))).toEqual({ title: 'Trip plans' })
  })

  it('deletes a chat after asking', async () => {
    const remove = vi.fn<() => unknown>(() => ({}))
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true)
    mockApi({
      'GET /api/orbs/o1': { id: 'o1', name: 'Pip' },
      'GET /api/conversations': [chat],
      'DELETE /api/conversations/c1': remove,
    })
    const user = userEvent.setup()
    renderRoute([{ path: '/orbs/:orbId/chats', element: <ChatsPage /> }], '/orbs/o1/chats')

    await user.click(await screen.findByRole('button', { name: 'Delete Old name' }))

    expect(confirm).toHaveBeenCalledWith('Delete "Old name"? This cannot be undone.')
    await vi.waitFor(() => expect(remove).toHaveBeenCalled())
    confirm.mockRestore()
  })
})
