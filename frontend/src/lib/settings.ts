// Device-local preferences. Nothing here is sent anywhere except the API token, to your own server.

export type ThemePreference = 'system' | 'dark' | 'light'

export interface Settings {
  theme: ThemePreference
  animations: boolean
  /** Base URL of the Orbs server. Empty means "same origin as this page". */
  serverUrl: string
  /** Optional shared secret, when the server sets Orbs__ApiToken. */
  apiToken: string
  /**
   * Session token for a server on another address (the mobile app). A server on the same address
   * uses an HttpOnly cookie instead, which scripts cannot read.
   */
  sessionToken: string
  /** The orb a new chat starts with. Empty means "the first orb". */
  lastOrbId: string
  /** Read replies aloud. */
  voiceReplies: boolean
}

export const DEFAULT_SETTINGS: Settings = {
  theme: 'system',
  animations: true,
  serverUrl: '',
  apiToken: '',
  sessionToken: '',
  lastOrbId: '',
  voiceReplies: false,
}

const STORAGE_KEY = 'orbs.settings.v1'

export function loadSettings(): Settings {
  try {
    const raw = globalThis.localStorage?.getItem(STORAGE_KEY)
    return raw ? { ...DEFAULT_SETTINGS, ...(JSON.parse(raw) as Partial<Settings>) } : DEFAULT_SETTINGS
  } catch {
    return DEFAULT_SETTINGS
  }
}

export function saveSettings(settings: Settings): void {
  globalThis.localStorage?.setItem(STORAGE_KEY, JSON.stringify(settings))
}
