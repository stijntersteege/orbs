import { useState, type FormEvent } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useSettings } from '@/app/SettingsContext'
import type { ThemePreference } from '@/lib/settings'
import { Button, Card, PageHeader, Switch, TextField } from '@/components/ui'
import styles from '../shared.module.css'

const THEMES: { id: ThemePreference; label: string }[] = [
  { id: 'light', label: 'Light' },
  { id: 'dark', label: 'Dark' },
  { id: 'system', label: 'Match my device' },
]

/** Device preferences and the server connection. Stored on this device only. */
export function SettingsPage() {
  const { settings, update } = useSettings()
  const client = useQueryClient()
  const [serverUrl, setServerUrl] = useState(settings.serverUrl)
  const [apiToken, setApiToken] = useState(settings.apiToken)
  const [saved, setSaved] = useState(false)

  const saveServer = (e: FormEvent) => {
    e.preventDefault()
    update({ serverUrl: serverUrl.trim(), apiToken: apiToken.trim() })
    void client.invalidateQueries()
    setSaved(true)
  }

  return (
    <>
      <PageHeader title="Settings" subtitle="These preferences live on this device." />
      <div className={styles.stack} style={{ maxWidth: 760 }}>
        <Card>
          <div className={styles.stack}>
            <h2>Appearance</h2>
            <fieldset className={styles.segmented}>
              <legend className="visually-hidden">Theme</legend>
              {THEMES.map((t) => (
                <label key={t.id} className={styles.segment}>
                  <input
                    type="radio"
                    name="theme"
                    className="visually-hidden"
                    checked={settings.theme === t.id}
                    onChange={() => update({ theme: t.id })}
                  />
                  {t.label}
                </label>
              ))}
            </fieldset>
            <Switch
              label="Orb animations"
              description="Floating and glow. Reduced-motion settings on your device always win."
              checked={settings.animations}
              onChange={(animations) => update({ animations })}
            />
          </div>
        </Card>

        <Card>
          <form className={styles.stack} onSubmit={saveServer} aria-label="Server">
            <h2>Server</h2>
            <p className={styles.muted}>
              Leave the address empty when this app is served by your Orbs server. The mobile app needs the
              full address, for example <span className="mono">https://orbs.example.com</span>.
            </p>
            <TextField
              label="Server address"
              type="url"
              value={serverUrl}
              placeholder="Same as this page"
              onChange={(e) => {
                setServerUrl(e.target.value)
                setSaved(false)
              }}
            />
            <TextField
              label="API token"
              type="password"
              autoComplete="off"
              value={apiToken}
              placeholder="Only if your server sets one"
              onChange={(e) => {
                setApiToken(e.target.value)
                setSaved(false)
              }}
            />
            <div className={styles.actions}>
              <Button type="submit" variant="primary">
                Save connection
              </Button>
              {saved && (
                <output className={styles.muted} style={{ alignSelf: 'center' }}>
                  Saved.
                </output>
              )}
            </div>
          </form>
        </Card>

        <Card>
          <div className={styles.stack}>
            <h2>About</h2>
            <p className={styles.muted}>
              Orbs v0.2.0 · open source under the MIT license. API reference at <a href="/scalar">/scalar</a>.
            </p>
          </div>
        </Card>
      </div>
    </>
  )
}
