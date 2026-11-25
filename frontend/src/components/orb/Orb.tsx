import type { CSSProperties } from 'react'
import { DEFAULT_APPEARANCE, type OrbAppearance } from '@/api/types'
import { ORB_PALETTE } from './palette'

interface OrbProps {
  appearance?: Partial<OrbAppearance>
  /** Rendered width and height in px. */
  size?: number
  /** Pulses the glow while the orb is thinking (a model is streaming). */
  thinking?: boolean
  /** Accessible name. Omit for decorative orbs next to a visible name. */
  label?: string
}

const BODY: Record<OrbAppearance['shape'], (tint: string) => React.ReactNode> = {
  circle: (tint) => <circle cx="60" cy="64" r="40" fill={tint} />,
  blob: (tint) => (
    <path
      d="M60 22 C88 22 102 42 100 66 C98 92 80 104 58 104 C34 104 18 90 20 64 C22 40 34 22 60 22Z"
      fill={tint}
    />
  ),
  squircle: (tint) => <rect x="20" y="24" width="80" height="80" rx="30" fill={tint} />,
  drop: (tint) => (
    <path
      d="M60 18 C60 18 98 50 98 74 C98 94 82 106 60 106 C38 106 22 94 22 74 C22 50 60 18 60 18Z"
      fill={tint}
    />
  ),
}

const stroke = (face: string) => ({
  fill: 'none',
  stroke: face,
  strokeWidth: 4,
  strokeLinecap: 'round' as const,
})

function Eyes({ eyes, face, highlight }: { eyes: OrbAppearance['eyes']; face: string; highlight: string }) {
  switch (eyes) {
    case 'dots':
      return (
        <>
          <circle cx="47" cy="62" r="5" fill={face} />
          <circle cx="73" cy="62" r="5" fill={face} />
        </>
      )
    case 'happy':
      return (
        <>
          <path d="M41 64 Q47 55 53 64" {...stroke(face)} />
          <path d="M67 64 Q73 55 79 64" {...stroke(face)} />
        </>
      )
    case 'wide':
      return (
        <>
          <circle cx="47" cy="61" r="9" fill="#FFFFFF" />
          <circle cx="73" cy="61" r="9" fill="#FFFFFF" />
          <circle cx="49" cy="62" r="4.5" fill="#1A1033" />
          <circle cx="75" cy="62" r="4.5" fill="#1A1033" />
        </>
      )
    case 'sleepy':
      return (
        <>
          <path d="M41 62 Q47 66 53 62" {...stroke(face)} />
          <path d="M67 62 Q73 66 79 62" {...stroke(face)} />
        </>
      )
    case 'wink':
      return (
        <>
          <circle cx="47" cy="62" r="5" fill={face} />
          <path d="M67 64 Q73 55 79 64" {...stroke(face)} />
        </>
      )
    case 'visor':
      return (
        <>
          <rect x="33" y="53" width="54" height="17" rx="8.5" fill="#1A1033" />
          <rect x="41" y="58.5" width="12" height="6" rx="3" fill={highlight} />
          <rect x="67" y="58.5" width="12" height="6" rx="3" fill={highlight} />
        </>
      )
  }
}

function Mouth({ mouth, face }: { mouth: OrbAppearance['mouth']; face: string }) {
  switch (mouth) {
    case 'smile':
      return <path d="M52 77 Q60 85 68 77" {...stroke(face)} />
    case 'open':
      return <ellipse cx="60" cy="80" rx="5" ry="6" fill={face} />
    case 'flat':
      return <path d="M54 80 L66 80" {...stroke(face)} />
    case 'grin':
      return (
        <path d="M48 76 Q60 92 72 76 Z" fill={face} stroke={face} strokeWidth={2} strokeLinejoin="round" />
      )
    case 'none':
      return null
  }
}

/** Accessories drawn behind the body. */
function BackAccessory({
  accessory,
  highlight,
}: {
  accessory: OrbAppearance['accessory']
  highlight: string
}) {
  if (accessory === 'ring')
    return (
      <ellipse
        cx="60"
        cy="66"
        rx="56"
        ry="13"
        transform="rotate(-12 60 66)"
        fill="none"
        stroke={highlight}
        strokeWidth={5}
      />
    )
  if (accessory === 'headphones')
    return (
      <path
        d="M24 60 A36 36 0 0 1 96 60"
        fill="none"
        stroke="#2A2150"
        strokeWidth={6}
        strokeLinecap="round"
      />
    )
  return null
}

/** Accessories drawn in front of the body. */
function FrontAccessory({
  accessory,
  highlight,
}: {
  accessory: OrbAppearance['accessory']
  highlight: string
}) {
  switch (accessory) {
    case 'ring':
      return (
        <path
          d="M4 66 A56 13 0 0 0 116 66"
          transform="rotate(-12 60 66)"
          fill="none"
          stroke={highlight}
          strokeWidth={5}
        />
      )
    case 'headphones':
      return (
        <>
          <rect x="14" y="50" width="15" height="26" rx="7.5" fill="#2A2150" />
          <rect x="91" y="50" width="15" height="26" rx="7.5" fill="#2A2150" />
        </>
      )
    case 'antenna':
      return (
        <>
          <path d="M60 26 L60 9" fill="none" stroke={highlight} strokeWidth={3.5} strokeLinecap="round" />
          <circle cx="60" cy="7" r="6" fill="#FFC53D" />
        </>
      )
    case 'halo':
      return <ellipse cx="60" cy="11" rx="22" ry="6" fill="none" stroke="#FFC53D" strokeWidth={4} />
    case 'sprout':
      return (
        <>
          <path d="M60 25 Q60 15 61 9" fill="none" stroke="#2EE59D" strokeWidth={3.5} strokeLinecap="round" />
          <path d="M61 13 Q71 2 80 8 Q72 19 61 13Z" fill="#2EE59D" />
          <path d="M60 17 Q49 8 42 14 Q50 23 60 17Z" fill="#2EE59D" />
        </>
      )
    case 'crown':
      return (
        <path
          d="M44 27 L46 11 L54 20 L60 6 L66 20 L74 11 L76 27 Z"
          fill="#FFC53D"
          stroke="#FFC53D"
          strokeWidth={2}
          strokeLinejoin="round"
        />
      )
    case 'none':
      return null
  }
}

/** The orb mascot: a model, its context and a face, drawn as a friendly SVG. */
export function Orb({ appearance, size = 64, thinking = false, label }: OrbProps) {
  const look = { ...DEFAULT_APPEARANCE, ...appearance }
  const { tint, highlight, face } = ORB_PALETTE[look.color]
  const wrapper: CSSProperties = { position: 'relative', width: size, height: size, flexShrink: 0 }
  const glow: CSSProperties = {
    position: 'absolute',
    inset: '18%',
    borderRadius: '50%',
    background: tint,
    opacity: 0.55,
    filter: `blur(${Math.max(4, Math.round(size / 7))}px)`,
    animation: thinking ? 'orb-think 1.2s ease-in-out infinite' : undefined,
  }
  const svg: CSSProperties = {
    position: 'absolute',
    inset: 0,
    overflow: 'visible',
    animation: look.floating ? 'orb-bob 2.8s ease-in-out infinite' : undefined,
  }

  return (
    <span
      style={wrapper}
      role={label ? 'img' : undefined}
      aria-label={label}
      aria-hidden={label ? undefined : true}
    >
      {(look.glow || thinking) && <span style={glow} data-testid="orb-glow" />}
      <svg viewBox="0 0 120 120" width="100%" height="100%" style={svg} aria-hidden="true">
        <BackAccessory accessory={look.accessory} highlight={highlight} />
        {BODY[look.shape](tint)}
        <ellipse
          cx="45"
          cy="47"
          rx="11"
          ry="6.5"
          transform="rotate(-32 45 47)"
          fill={highlight}
          opacity={0.95}
        />
        <circle cx="36" cy="58" r="2.6" fill="#FFFFFF" opacity={0.85} />
        {look.blush && (
          <>
            <ellipse cx="38" cy="74" rx="6" ry="3.5" fill="#FF7AC6" opacity={0.6} />
            <ellipse cx="82" cy="74" rx="6" ry="3.5" fill="#FF7AC6" opacity={0.6} />
          </>
        )}
        <Eyes eyes={look.eyes} face={face} highlight={highlight} />
        <Mouth mouth={look.mouth} face={face} />
        <FrontAccessory accessory={look.accessory} highlight={highlight} />
      </svg>
    </span>
  )
}
