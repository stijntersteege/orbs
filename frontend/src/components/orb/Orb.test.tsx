import { render, screen } from '@testing-library/react'
import { ORB_ACCESSORIES, ORB_COLORS, ORB_EYES, ORB_MOUTHS, ORB_SHAPES } from '@/api/types'
import { Orb } from './Orb'
import { ORB_PALETTE } from './palette'

describe('Orb', () => {
  it('is decorative by default', () => {
    const { container } = render(<Orb />)
    expect(container.firstElementChild).toHaveAttribute('aria-hidden', 'true')
  })

  it('becomes an image with a name when labelled', () => {
    render(<Orb label="Pip" />)
    expect(screen.getByRole('img', { name: 'Pip' })).toBeInTheDocument()
  })

  it('uses the color of its palette', () => {
    const { container } = render(<Orb appearance={{ color: 'mint', shape: 'circle' }} />)
    expect(container.querySelector('circle[r="40"]')).toHaveAttribute('fill', ORB_PALETTE.mint.tint)
  })

  it('shows the glow only when asked or while thinking', () => {
    const { rerender } = render(<Orb appearance={{ glow: false }} />)
    expect(screen.queryByTestId('orb-glow')).not.toBeInTheDocument()

    rerender(<Orb appearance={{ glow: false }} thinking />)
    expect(screen.getByTestId('orb-glow')).toBeInTheDocument()
  })

  it('shows each state on its face', () => {
    const { container, rerender } = render(
      <Orb appearance={{ eyes: 'dots', mouth: 'none' }} state="listening" />,
    )
    expect(screen.getByTestId('orb-ring')).toBeInTheDocument()
    expect(container.firstElementChild).toHaveAttribute('data-state', 'listening')

    rerender(<Orb appearance={{ mouth: 'none' }} state="speaking" />)
    expect(screen.getByTestId('orb-mouth').querySelector('ellipse')).not.toBeNull()
    expect(screen.getByTestId('orb-mouth')).toHaveStyle({
      animation: 'orb-speak 0.32s ease-in-out infinite alternate',
    })

    rerender(<Orb state="waiting" />)
    expect(screen.getByTestId('orb-question')).toBeInTheDocument()
    expect(screen.queryByTestId('orb-ring')).not.toBeInTheDocument()
  })

  it('treats the old thinking flag as the thinking state', () => {
    const { container } = render(<Orb thinking />)
    expect(container.firstElementChild).toHaveAttribute('data-state', 'thinking')
  })

  const parts = [
    ...ORB_COLORS.map((color) => ({ color })),
    ...ORB_SHAPES.map((shape) => ({ shape })),
    ...ORB_EYES.map((eyes) => ({ eyes })),
    ...ORB_MOUTHS.map((mouth) => ({ mouth })),
    ...ORB_ACCESSORIES.map((accessory) => ({ accessory })),
  ]

  it.each(parts)('draws %o', (part) => {
    const { container } = render(<Orb appearance={part} />)
    expect(container.querySelectorAll('svg *').length).toBeGreaterThan(3)
  })
})
