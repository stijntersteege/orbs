import { DEFAULT_APPEARANCE } from '@/api/types'
import { orbSvg } from './orbImage'

describe('orbSvg', () => {
  it('makes a standalone SVG document of the face', () => {
    const svg = orbSvg(DEFAULT_APPEARANCE)

    expect(svg.startsWith('<svg xmlns="http://www.w3.org/2000/svg"')).toBe(true)
    expect(svg).toContain('width="512" height="512"')
    expect(svg).not.toContain('animation')
    expect(svg.endsWith('</svg>')).toBe(true)
  })
})
