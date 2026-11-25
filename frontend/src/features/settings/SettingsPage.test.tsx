import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { loadSettings } from '@/lib/settings'
import { renderPage } from '@/test/render'
import { SettingsPage } from './SettingsPage'

describe('SettingsPage', () => {
  it('switches the theme on the document and remembers it', async () => {
    const user = userEvent.setup()
    renderPage(<SettingsPage />)

    await user.click(screen.getByRole('radio', { name: 'Light' }))

    expect(document.documentElement.dataset.theme).toBe('light')
    expect(loadSettings().theme).toBe('light')
  })

  it('saves the server connection', async () => {
    const user = userEvent.setup()
    renderPage(<SettingsPage />)

    await user.type(screen.getByLabelText('Server address'), 'https://orbs.example.com')
    await user.type(screen.getByLabelText('API token'), 'secret')
    await user.click(screen.getByRole('button', { name: 'Save connection' }))

    expect(screen.getByText('Saved.')).toBeInTheDocument()
    expect(loadSettings()).toMatchObject({ serverUrl: 'https://orbs.example.com', apiToken: 'secret' })
  })

  it('turns orb animations off', async () => {
    const user = userEvent.setup()
    renderPage(<SettingsPage />)

    await user.click(screen.getByRole('switch', { name: /Orb animations/ }))

    expect(document.documentElement.dataset.motion).toBe('off')
  })
})
