import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DEFAULT_APPEARANCE, ORB_COLORS, type OrbAppearance } from '@/api/types'
import { OrbMaker, randomAppearance } from './OrbMaker'

function Harness({ onChange }: { onChange: (a: OrbAppearance) => void }) {
  const [value, setValue] = useState(DEFAULT_APPEARANCE)
  return (
    <OrbMaker
      value={value}
      onChange={(a) => {
        setValue(a)
        onChange(a)
      }}
    />
  )
}

describe('OrbMaker', () => {
  it('picks a color, shape and accessory', async () => {
    const user = userEvent.setup()
    const changes: OrbAppearance[] = []
    render(<Harness onChange={(a) => changes.push(a)} />)

    await user.click(screen.getByRole('button', { name: 'Color: Mint' }))
    await user.click(screen.getByRole('button', { name: 'Shape: squircle' }))
    await user.click(screen.getByRole('button', { name: 'Accessory: crown' }))

    expect(changes.at(-1)).toMatchObject({ color: 'mint', shape: 'squircle', accessory: 'crown' })
    expect(screen.getByRole('button', { name: 'Color: Mint' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('toggles blush, glow and float', async () => {
    const user = userEvent.setup()
    const changes: OrbAppearance[] = []
    render(<Harness onChange={(a) => changes.push(a)} />)

    await user.click(screen.getByRole('button', { name: 'Blush' }))

    expect(changes.at(-1)?.blush).toBe(!DEFAULT_APPEARANCE.blush)
  })
})

describe('randomAppearance', () => {
  it('always returns valid parts and keeps glow and float', () => {
    for (let i = 0; i < 50; i++) {
      const look = randomAppearance({ ...DEFAULT_APPEARANCE, glow: false, floating: false })
      expect(ORB_COLORS).toContain(look.color)
      expect(look.glow).toBe(false)
      expect(look.floating).toBe(false)
    }
  })
})
