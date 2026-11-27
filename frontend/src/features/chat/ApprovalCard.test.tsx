import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApprovalCard } from './ApprovalCard'

describe('ApprovalCard', () => {
  it('shows what the orb wants to do and passes on the answer', async () => {
    const onDecide = vi.fn<(d: string) => void>()
    render(
      <ApprovalCard
        orbName="Pip"
        approval={{ id: 'a1', callId: 'c1', tool: 'calculator', arguments: '{"expression":"2+2"}' }}
        onDecide={onDecide}
      />,
    )

    expect(screen.getByRole('region', { name: 'Pip wants to use calculator' })).toBeInTheDocument()
    expect(screen.getByText(/"expression": "2\+2"/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Always allow' }))
    await userEvent.click(screen.getByRole('button', { name: 'Decline' }))

    expect(onDecide.mock.calls).toEqual([['always'], ['deny']])
  })
})
