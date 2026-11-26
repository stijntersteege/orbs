# Design system

Orbs is **playful, never childish**: the joy lives in the orbs and the color, while the interface around them stays calm, legible and fast.

## Tokens

All colors, radii, spacing and motion live in `frontend/src/styles/tokens.css` as CSS custom properties. Components never hard-code interface colors.

### Orb palette (same in both themes)

| Name | Tint | Highlight | Use |
| --- | --- | --- | --- |
| Plasma | `#7C4DFF` | `#C9B8FF` | Brand, primary actions |
| Ocean | `#3D5BFF` | `#A9B7FF` | Links, focus rings |
| Sky | `#4CC9FF` | `#B5E9FF` | Info |
| Mint | `#2EE59D` | `#A8F7D6` | Success, connected |
| Sun | `#FFC53D` | `#FFE7A3` | Highlights, warnings |
| Coral | `#FF6B5B` | `#FFB3A8` | Errors, stop |
| Bubblegum | `#FF7AC6` | `#FFC4E6` | Delight, new things |

Text on an orb color is white (Plasma, Ocean) or Void `#1A1033` (all others) for contrast.

### Theme tokens

`--bg`, `--side`, `--surface`, `--card`, `--raised`, `--sel`, `--chip`, `--line`, `--line2`, `--muted`, `--faint`, `--text`, `--accent-text`, status colors (`--ok`, `--warn`, `--err`, `--info` and their `-bg` variants) and orb washes. Both themes are clean neutrals with a hint of violet, so the orb colors are the only real accents: the light theme is near-white (`#FDFDFD`), the dark theme near-black ink (`#0B0A12`). The theme is set with `data-theme` on `<html>`.

## Type

| Family | Use |
| --- | --- |
| **Geist** | Headings (600, tight tracking), interface and reading |
| **Geist Mono** | Model ids, context locations, code, section numbers |

Fonts are bundled with the app (Fontsource), so nothing loads from third-party servers.

## Shape and motion

- Soft but quiet: buttons and inputs 12 px, cards 20 px, large panels 24 px, small chips and tags pills. Orbs are the only true circles.
- One-pixel borders in `--line`; shadows only on large showcase panels.
- Spacing on a 4 px grid.
- Motion: `bob` 2.8 s float for idle orbs, `think` 1.2 s glow pulse while a model streams, `pop` 240 ms for creation, `settle` 160 ms for everything else.
- Reduced motion (device setting or the in-app switch) turns movement off; the glow stays.

## The orb mascot

`frontend/src/components/orb/Orb.tsx` draws an orb from its appearance: color, shape (circle, blob, squircle, drop), eyes (dots, happy, wide, sleepy, wink, visor), mouth (smile, open, flat, grin, none), accessory (none, antenna, halo, sprout, ring, headphones, crown), plus blush, glow and float. Pass `label` when the orb is meaningful on its own; otherwise it is decorative.

## Accessibility rules

- Use real elements: `button`, `a`, `label` + `input`. Never click handlers on `div`s.
- Icon-only buttons need an `aria-label`.
- Text contrast at least 4.5:1, also for small grey labels.
- Touch targets at least 44 px.
- Colors that must be told apart also differ in lightness, not hue alone.
- Streaming replies are announced politely (`aria-live`).
