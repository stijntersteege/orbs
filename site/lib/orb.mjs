// The orb mascot as plain SVG markup, for the static website.
// Mirrors frontend/src/components/orb/Orb.tsx and palette.ts, so the site and the app draw the same orbs.

export const PALETTE = {
  plasma: { tint: '#7C4DFF', highlight: '#C9B8FF', face: '#FFFFFF' },
  ocean: { tint: '#3D5BFF', highlight: '#A9B7FF', face: '#FFFFFF' },
  sky: { tint: '#4CC9FF', highlight: '#B5E9FF', face: '#1A1033' },
  mint: { tint: '#2EE59D', highlight: '#A8F7D6', face: '#1A1033' },
  sun: { tint: '#FFC53D', highlight: '#FFE7A3', face: '#1A1033' },
  coral: { tint: '#FF6B5B', highlight: '#FFB3A8', face: '#1A1033' },
  bubblegum: { tint: '#FF7AC6', highlight: '#FFC4E6', face: '#1A1033' },
}

const line = (face) => `fill="none" stroke="${face}" stroke-width="4" stroke-linecap="round"`

const BODY = {
  circle: (t) => `<circle cx="60" cy="64" r="40" fill="${t}"/>`,
  blob: (t) =>
    `<path d="M60 22 C88 22 102 42 100 66 C98 92 80 104 58 104 C34 104 18 90 20 64 C22 40 34 22 60 22Z" fill="${t}"/>`,
  squircle: (t) => `<rect x="20" y="24" width="80" height="80" rx="30" fill="${t}"/>`,
  drop: (t) =>
    `<path d="M60 18 C60 18 98 50 98 74 C98 94 82 106 60 106 C38 106 22 94 22 74 C22 50 60 18 60 18Z" fill="${t}"/>`,
}

const EYES = {
  dots: (f) => `<circle cx="47" cy="62" r="5" fill="${f}"/><circle cx="73" cy="62" r="5" fill="${f}"/>`,
  happy: (f) => `<path d="M41 64 Q47 55 53 64" ${line(f)}/><path d="M67 64 Q73 55 79 64" ${line(f)}/>`,
  wide: () =>
    '<circle cx="47" cy="61" r="9" fill="#FFFFFF"/><circle cx="73" cy="61" r="9" fill="#FFFFFF"/>' +
    '<circle cx="49" cy="62" r="4.5" fill="#1A1033"/><circle cx="75" cy="62" r="4.5" fill="#1A1033"/>',
  sleepy: (f) => `<path d="M41 62 Q47 66 53 62" ${line(f)}/><path d="M67 62 Q73 66 79 62" ${line(f)}/>`,
  wink: (f) => `<circle cx="47" cy="62" r="5" fill="${f}"/><path d="M67 64 Q73 55 79 64" ${line(f)}/>`,
  visor: (_f, h) =>
    '<rect x="33" y="53" width="54" height="17" rx="8.5" fill="#1A1033"/>' +
    `<rect x="41" y="58.5" width="12" height="6" rx="3" fill="${h}"/><rect x="67" y="58.5" width="12" height="6" rx="3" fill="${h}"/>`,
}

const MOUTH = {
  smile: (f) => `<path d="M52 77 Q60 85 68 77" ${line(f)}/>`,
  open: (f) => `<ellipse cx="60" cy="80" rx="5" ry="6" fill="${f}"/>`,
  flat: (f) => `<path d="M54 80 L66 80" ${line(f)}/>`,
  grin: (f) => `<path d="M48 76 Q60 92 72 76 Z" fill="${f}" stroke="${f}" stroke-width="2" stroke-linejoin="round"/>`,
  none: () => '',
}

const BACK = {
  ring: (h) =>
    `<ellipse cx="60" cy="66" rx="56" ry="13" transform="rotate(-12 60 66)" fill="none" stroke="${h}" stroke-width="5"/>`,
  headphones: () =>
    '<path d="M24 60 A36 36 0 0 1 96 60" fill="none" stroke="#2A2150" stroke-width="6" stroke-linecap="round"/>',
}

const FRONT = {
  ring: (h) =>
    `<path d="M4 66 A56 13 0 0 0 116 66" transform="rotate(-12 60 66)" fill="none" stroke="${h}" stroke-width="5"/>`,
  headphones: () =>
    '<rect x="14" y="50" width="15" height="26" rx="7.5" fill="#2A2150"/><rect x="91" y="50" width="15" height="26" rx="7.5" fill="#2A2150"/>',
  antenna: (h) =>
    `<path d="M60 26 L60 9" fill="none" stroke="${h}" stroke-width="3.5" stroke-linecap="round"/><circle cx="60" cy="7" r="6" fill="#FFC53D"/>`,
  halo: () => '<ellipse cx="60" cy="11" rx="22" ry="6" fill="none" stroke="#FFC53D" stroke-width="4"/>',
  sprout: () =>
    '<path d="M60 25 Q60 15 61 9" fill="none" stroke="#2EE59D" stroke-width="3.5" stroke-linecap="round"/>' +
    '<path d="M61 13 Q71 2 80 8 Q72 19 61 13Z" fill="#2EE59D"/><path d="M60 17 Q49 8 42 14 Q50 23 60 17Z" fill="#2EE59D"/>',
  crown: () =>
    '<path d="M44 27 L46 11 L54 20 L60 6 L66 20 L74 11 L76 27 Z" fill="#FFC53D" stroke="#FFC53D" stroke-width="2" stroke-linejoin="round"/>',
  none: () => '',
}

/**
 * Renders one orb. Options match the app's appearance fields:
 * color, shape, eyes, mouth, accessory, blush, glow, floating, plus size (px) and an optional label.
 */
export function orb({
  color = 'plasma',
  shape = 'circle',
  eyes = 'happy',
  mouth = 'smile',
  accessory = 'antenna',
  blush = true,
  glow = true,
  floating = false,
  size = 64,
  label,
  className = '',
} = {}) {
  const { tint, highlight, face } = PALETTE[color]
  const a11y = label ? `role="img" aria-label="${label}"` : 'aria-hidden="true"'
  const blushes = blush
    ? '<ellipse cx="38" cy="74" rx="6" ry="3.5" fill="#FF7AC6" opacity="0.6"/><ellipse cx="82" cy="74" rx="6" ry="3.5" fill="#FF7AC6" opacity="0.6"/>'
    : ''
  const blur = Math.max(4, Math.round(size / 7))
  const glowSpan = glow ? `<span class="orb-glow" style="background:${tint};filter:blur(${blur}px)"></span>` : ''
  return (
    `<span class="orb${floating ? ' orb-float' : ''}${className ? ` ${className}` : ''}" style="width:${size}px;height:${size}px" ${a11y}>` +
    glowSpan +
    '<svg viewBox="0 0 120 120" aria-hidden="true">' +
    (BACK[accessory]?.(highlight) ?? '') +
    BODY[shape](tint) +
    `<ellipse cx="45" cy="47" rx="11" ry="6.5" transform="rotate(-32 45 47)" fill="${highlight}" opacity="0.95"/>` +
    '<circle cx="36" cy="58" r="2.6" fill="#FFFFFF" opacity="0.85"/>' +
    blushes +
    EYES[eyes](face, highlight) +
    MOUTH[mouth](face) +
    FRONT[accessory](highlight) +
    '</svg></span>'
  )
}
