/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** The app version from package.json, set at build time. */
  readonly VITE_ORBS_VERSION: string
  /** Optional default API base URL, baked in at build time (for example for the mobile app). */
  readonly VITE_API_URL?: string
}
