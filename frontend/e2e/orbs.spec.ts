import { expect, test } from '@playwright/test'

// Runs against a fresh stack with demo data (Orbs__SeedDemoData=true): one Echo provider and an orb called Pip.

test('a first run opens a chat with the demo orb', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'What can Pip help with?' })).toBeVisible()
  await expect(page.getByRole('button', { name: /^Model:/ })).toBeVisible()
})

test('orbs stay in the sidebar, everything else sits behind the menu', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 })
  await page.goto('/')
  await expect(page.getByRole('navigation', { name: 'Your orbs' }).getByText('Pip')).toBeVisible()
  await page.getByRole('button', { name: 'Settings and more' }).click()
  await page
    .getByRole('navigation', { name: 'Menu' })
    .getByRole('link', { name: /Manage orbs/ })
    .click()
  await expect(page.getByRole('heading', { name: 'Your orbs' })).toBeVisible()
  await expect(page.getByRole('list', { name: 'Orbs' }).getByText('Pip')).toBeVisible()
})

test('people can create an orb and talk to it', async ({ page }) => {
  const name = `Test ${Date.now() % 100000}`

  await page.goto('/orbs/new')
  await page.getByLabel('Name').fill(name)
  await page.getByRole('button', { name: 'Color: Coral' }).click()
  await page.getByRole('button', { name: `Create ${name}` }).click()

  await expect(page).toHaveURL(/\/orbs\/[^/]+\/chat$/)
  await expect(page.getByRole('heading', { name: `What can ${name} help with?` })).toBeVisible()
  await page.getByLabel(`Message ${name}`).fill('Hello from Playwright')
  await page.getByRole('button', { name: 'Send' }).click()

  await expect(page.getByText('You said: Hello from Playwright')).toBeVisible()
  // Narrow screens open the activity panel from the header; wide ones always show it.
  const toggle = page.getByRole('button', { name: 'Activity', exact: true })
  if (await toggle.isVisible()) await toggle.click()
  await expect(
    page.getByRole('complementary', { name: 'Activity' }).getByText(/^Answered with /),
  ).toBeVisible()
})

test('the theme can be switched', async ({ page }) => {
  await page.goto('/settings')
  await page.getByText('Light', { exact: true }).click()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light')
})
