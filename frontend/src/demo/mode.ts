/** True in the static live demo build (`vite build --mode demo`), where the API runs in the browser. */
export const IS_DEMO = import.meta.env.MODE === 'demo'

/** Where the demo banner points people who want the real thing. */
export const REPOSITORY_URL = 'https://github.com/stijntersteege/orbs'

export const DEMO_STORAGE_KEY = 'orbs.demo.v1'
