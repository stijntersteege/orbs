import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import type { OrbAppearance } from '@/api/types'
import { Orb } from '@/components/orb/Orb'

/** The orb's face as a standalone SVG document, without glow or animation. */
export function orbSvg(appearance: OrbAppearance): string {
  const markup = renderToStaticMarkup(
    createElement(Orb, { appearance: { ...appearance, glow: false, floating: false }, size: 512 }),
  )
  const svg = markup.slice(markup.indexOf('<svg'), markup.lastIndexOf('</svg>') + 6)
  return svg
    .replace('<svg', '<svg xmlns="http://www.w3.org/2000/svg"')
    .replace(/ style="[^"]*"/, '')
    .replace('width="100%" height="100%"', 'width="512" height="512"')
}

/** The orb's face as a square PNG on a soft background, for bot profile pictures. */
export async function orbPng(appearance: OrbAppearance, size = 512): Promise<Blob> {
  const url = URL.createObjectURL(new Blob([orbSvg(appearance)], { type: 'image/svg+xml' }))
  try {
    const image = new Image()
    image.src = url
    await image.decode()
    const canvas = document.createElement('canvas')
    canvas.width = size
    canvas.height = size
    const context = canvas.getContext('2d')
    if (!context) throw new Error('This browser cannot draw pictures.')
    context.fillStyle = '#F4F2FA'
    context.fillRect(0, 0, size, size)
    const inset = size * 0.08
    context.drawImage(image, inset, inset, size - inset * 2, size - inset * 2)
    return await new Promise<Blob>((resolve, reject) =>
      canvas.toBlob(
        (blob) => (blob ? resolve(blob) : reject(new Error('Could not make the picture.'))),
        'image/png',
      ),
    )
  } finally {
    URL.revokeObjectURL(url)
  }
}
