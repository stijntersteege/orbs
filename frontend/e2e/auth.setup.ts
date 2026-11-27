import { expect, test as setup } from '@playwright/test'

// Signs in once (creating the first account on a fresh stack) and shares the session with every test.
const STORAGE_STATE = 'e2e/.auth/state.json'
const account = { email: 'e2e@orbs.test', displayName: 'E2E', password: 'playwright passes here' }

setup('sign in', async ({ request }) => {
  const status = (await (await request.get('/api/auth/status')).json()) as { setupRequired: boolean }
  const response = status.setupRequired
    ? await request.post('/api/auth/setup', { data: account })
    : await request.post('/api/auth/sign-in', { data: { email: account.email, password: account.password } })
  expect(response.ok()).toBeTruthy()
  await request.storageState({ path: STORAGE_STATE })
})
