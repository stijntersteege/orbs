import type { CapacitorConfig } from '@capacitor/cli'

// Native shells (iOS / Android) wrap the same web build. Point the app at your
// Orbs server under Settings > Server on first launch.
const config: CapacitorConfig = {
  appId: 'org.orbs.app',
  appName: 'Orbs',
  webDir: 'dist',
  backgroundColor: '#0F0C1D',
}

export default config
