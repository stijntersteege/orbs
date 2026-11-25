import {
  ORB_ACCESSORIES,
  ORB_COLORS,
  ORB_EYES,
  ORB_MOUTHS,
  ORB_SHAPES,
  type OrbAppearance,
} from '@/api/types'
import { Orb } from '@/components/orb/Orb'
import { ORB_PALETTE } from '@/components/orb/palette'
import { Button, ChoiceGroup } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import styles from './orbs.module.css'

const pick = <T,>(list: readonly T[]): T => list[Math.floor(Math.random() * list.length)]!

/** Rolls a random look. Exported for tests. */
export function randomAppearance(current: OrbAppearance): OrbAppearance {
  return {
    ...current,
    color: pick(ORB_COLORS),
    shape: pick(ORB_SHAPES),
    eyes: pick(ORB_EYES),
    mouth: pick(ORB_MOUTHS),
    accessory: pick(ORB_ACCESSORIES),
    blush: Math.random() > 0.5,
  }
}

interface OrbMakerProps {
  value: OrbAppearance
  onChange: (value: OrbAppearance) => void
}

/** Customizing an orb's face: color, shape, eyes, mouth, accessory and a few toggles. */
export function OrbMaker({ value, onChange }: OrbMakerProps) {
  const set = <K extends keyof OrbAppearance>(key: K, v: OrbAppearance[K]) => onChange({ ...value, [key]: v })
  const tile = (patch: Partial<OrbAppearance>) => (
    <Orb size={48} appearance={{ ...value, ...patch, glow: false, floating: false }} />
  )

  return (
    <div className={styles.stack}>
      <fieldset
        style={{ border: 0, padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 10 }}
      >
        <legend style={{ fontWeight: 700, fontSize: 14, marginBottom: 10 }}>Color</legend>
        <div className={styles.swatches}>
          {ORB_COLORS.map((color) => {
            const p = ORB_PALETTE[color]
            return (
              <button
                key={color}
                type="button"
                className={styles.swatch}
                aria-label={`Color: ${p.label}`}
                aria-pressed={value.color === color}
                style={{ background: `radial-gradient(circle at 32% 28%, ${p.highlight} 0%, ${p.tint} 60%)` }}
                onClick={() => set('color', color)}
              />
            )
          })}
        </div>
      </fieldset>

      <ChoiceGroup
        label="Shape"
        options={ORB_SHAPES}
        value={value.shape}
        onChange={(v) => set('shape', v)}
        render={(shape) => tile({ shape })}
      />
      <ChoiceGroup
        label="Eyes"
        options={ORB_EYES}
        value={value.eyes}
        onChange={(v) => set('eyes', v)}
        render={(eyes) => tile({ eyes, accessory: 'none' })}
      />
      <ChoiceGroup
        label="Mouth"
        options={ORB_MOUTHS}
        value={value.mouth}
        onChange={(v) => set('mouth', v)}
        render={(mouth) => tile({ mouth, accessory: 'none' })}
      />
      <ChoiceGroup
        label="Accessory"
        options={ORB_ACCESSORIES}
        value={value.accessory}
        onChange={(v) => set('accessory', v)}
        render={(accessory) => tile({ accessory })}
      />

      <div className={styles.toggles}>
        {(
          [
            ['blush', 'Blush'],
            ['glow', 'Glow'],
            ['floating', 'Float'],
          ] as const
        ).map(([key, label]) => (
          <button
            key={key}
            type="button"
            className={styles.toggle}
            aria-pressed={value[key]}
            onClick={() => set(key, !value[key])}
          >
            {label}
          </button>
        ))}
        <Button onClick={() => onChange(randomAppearance(value))}>
          <Icon name="shuffle" size={18} /> Surprise me
        </Button>
      </div>
    </div>
  )
}
