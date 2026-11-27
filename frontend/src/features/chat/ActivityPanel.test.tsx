import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { mockApi, renderPage } from '@/test/render'
import { ActivityPanel } from './ActivityPanel'
import type { ActivityEntry } from './useChat'

const reply: ActivityEntry = {
  id: 'm1',
  model: 'echo-1',
  sources: [],
  startedAt: 0,
  finishedAt: 1000,
  tools: [{ callId: 'c1', tool: 'calculator', arguments: '{}', result: '42', isError: false }],
}

describe('ActivityPanel', () => {
  it('saves the steps of a reply as a skill', async () => {
    const save = vi.fn<(init?: RequestInit) => unknown>(() => ({ id: 'k1', name: 'do-math' }))
    mockApi({ 'POST /api/conversations/c1/messages/m1/skill': save })
    const user = userEvent.setup()
    renderPage(
      <ActivityPanel
        orb={undefined}
        activity={[reply]}
        known={[]}
        conversationId="c1"
        open
        onClose={() => {}}
      />,
    )

    await user.click(screen.getByRole('button', { name: 'Save these steps as a skill' }))
    await user.type(screen.getByLabelText('Skill name'), 'Do math')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByRole('link', { name: 'do-math' })).toHaveAttribute('href', '/skills')
    expect(JSON.parse(String(save.mock.calls[0]![0]?.body))).toEqual({ name: 'Do math' })
  })

  it('offers no skill for a reply without tools', () => {
    mockApi({})
    renderPage(
      <ActivityPanel
        orb={undefined}
        activity={[{ ...reply, tools: [] }]}
        known={[]}
        conversationId="c1"
        open
        onClose={() => {}}
      />,
    )

    expect(screen.queryByRole('button', { name: 'Save these steps as a skill' })).not.toBeInTheDocument()
  })
})
