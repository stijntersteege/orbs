import { useId, useState } from 'react'
import { voiceApi } from '@/api/endpoints'
import { useVoiceStatus } from '@/api/queries'
import { Button, ErrorNotice, TextField } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import styles from './orbs.module.css'

/** Chooses how the orb sounds when it reads replies aloud, with a preview. */
export function VoicePicker({
  name,
  value,
  onChange,
}: {
  name: string
  value: string
  onChange: (voice: string) => void
}) {
  const { data: status } = useVoiceStatus()
  const listId = useId()
  const [error, setError] = useState<unknown>(null)
  const [playing, setPlaying] = useState(false)

  if (!status?.canSpeak) {
    return (
      <p className={styles.muted}>
        To give {name} a voice, set a text-to-speech model on one of your providers under Models.
      </p>
    )
  }

  const preview = async () => {
    setError(null)
    setPlaying(true)
    try {
      const audio = new Audio(
        URL.createObjectURL(
          await voiceApi.speak(`Hi, I am ${name}. This is how I sound.`, value || undefined),
        ),
      )
      audio.onended = () => setPlaying(false)
      await audio.play()
    } catch (e) {
      setError(e)
      setPlaying(false)
    }
  }

  return (
    <div className={styles.stack}>
      <div className={styles.voiceRow}>
        <TextField
          label="Voice"
          list={listId}
          value={value}
          placeholder={status.voices[0] ?? 'The provider default'}
          onChange={(e) => onChange(e.target.value)}
          hint="Pick a suggestion or type any voice your speech model knows, such as af_heart for Kokoro."
        />
        <Button onClick={() => void preview()} disabled={playing} style={{ alignSelf: 'center' }}>
          <Icon name="speaker" size={16} /> Preview
        </Button>
      </div>
      <datalist id={listId}>
        {status.voices.map((v) => (
          <option key={v} value={v}>
            {v}
          </option>
        ))}
      </datalist>
      <ErrorNotice error={error} />
    </div>
  )
}
