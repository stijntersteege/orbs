import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { DEFAULT_SETTINGS, loadSettings, saveSettings, type Settings } from '@/lib/settings'

interface SettingsContextValue {
  settings: Settings
  update: (patch: Partial<Settings>) => void
}

const SettingsContext = createContext<SettingsContextValue>({ settings: DEFAULT_SETTINGS, update: () => {} })

function prefersDark(): boolean {
  return globalThis.matchMedia?.('(prefers-color-scheme: dark)').matches ?? true
}

/** Holds device settings and applies theme and motion to the document. */
export function SettingsProvider({ children }: { children: ReactNode }) {
  const [settings, setSettings] = useState<Settings>(loadSettings)

  const update = useCallback((patch: Partial<Settings>) => {
    setSettings((current) => {
      const next = { ...current, ...patch }
      saveSettings(next)
      return next
    })
  }, [])

  useEffect(() => {
    const apply = () => {
      const theme = settings.theme === 'system' ? (prefersDark() ? 'dark' : 'light') : settings.theme
      document.documentElement.dataset.theme = theme
      document.documentElement.dataset.motion = settings.animations ? 'on' : 'off'
    }
    apply()
    const media = globalThis.matchMedia?.('(prefers-color-scheme: dark)')
    media?.addEventListener('change', apply)
    return () => media?.removeEventListener('change', apply)
  }, [settings.theme, settings.animations])

  const value = useMemo(() => ({ settings, update }), [settings, update])
  return <SettingsContext.Provider value={value}>{children}</SettingsContext.Provider>
}

export const useSettings = () => useContext(SettingsContext)
