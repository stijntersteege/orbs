import type { OrbColor } from '@/api/types'

export interface OrbPalette {
  label: string
  tint: string
  highlight: string
  /** Face color with enough contrast on the tint: white or Void. */
  face: string
}

export const ORB_PALETTE: Record<OrbColor, OrbPalette> = {
  plasma: { label: 'Plasma', tint: '#7C4DFF', highlight: '#C9B8FF', face: '#FFFFFF' },
  ocean: { label: 'Ocean', tint: '#3D5BFF', highlight: '#A9B7FF', face: '#FFFFFF' },
  sky: { label: 'Sky', tint: '#4CC9FF', highlight: '#B5E9FF', face: '#1A1033' },
  mint: { label: 'Mint', tint: '#2EE59D', highlight: '#A8F7D6', face: '#1A1033' },
  sun: { label: 'Sun', tint: '#FFC53D', highlight: '#FFE7A3', face: '#1A1033' },
  coral: { label: 'Coral', tint: '#FF6B5B', highlight: '#FFB3A8', face: '#1A1033' },
  bubblegum: { label: 'Bubblegum', tint: '#FF7AC6', highlight: '#FFC4E6', face: '#1A1033' },
}

/** The soft tile color behind an orb on cards, per theme via CSS variables. */
export const ORB_WASH: Record<OrbColor, string> = {
  plasma: 'var(--wash-plasma)',
  ocean: 'var(--wash-plasma)',
  sky: 'var(--wash-sky)',
  mint: 'var(--wash-mint)',
  sun: 'var(--wash-sun)',
  coral: 'var(--wash-gum)',
  bubblegum: 'var(--wash-gum)',
}
