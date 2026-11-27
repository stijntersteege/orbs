import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { Orb } from '@/api/types'
import { DEFAULT_APPEARANCE } from '@/api/types'
import { mockApi, renderPage } from '@/test/render'
import { OrbsPage } from './OrbsPage'

const pip: Orb = {
  id: 'o1',
  name: 'Pip',
  description: 'Work sidekick',
  instructions: '',
  appearance: DEFAULT_APPEARANCE,
  contextSourceIds: ['s1'],
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

describe('OrbsPage', () => {
  it('lists orbs with their sources and model', async () => {
    mockApi({
      'GET /api/orbs': [pip],
      'GET /api/providers': [{ id: 'p1' }],
      'GET /api/context-sources': [{ id: 's1' }],
    })

    renderPage(<OrbsPage />)

    expect(await screen.findByText('Pip')).toBeInTheDocument()
    expect(screen.getByText('Work sidekick')).toBeInTheDocument()
    expect(screen.getByText('1 source')).toBeInTheDocument()
    expect(screen.getByText('auto')).toBeInTheDocument()
    expect(await screen.findByText('1 orb · 1 source connected · 1 provider')).toBeInTheDocument()
  })

  it('invites people to connect a model when none is connected', async () => {
    mockApi({ 'GET /api/orbs': [pip], 'GET /api/providers': [], 'GET /api/context-sources': [] })

    renderPage(<OrbsPage />)

    expect(await screen.findByRole('heading', { name: 'Give your orbs a brain' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Connect a model' })).toHaveAttribute('href', '/models')
  })

  it('shows an empty state for a first run', async () => {
    mockApi({ 'GET /api/orbs': [], 'GET /api/providers': [{ id: 'p1' }], 'GET /api/context-sources': [] })

    renderPage(<OrbsPage />)

    expect(await screen.findByRole('link', { name: 'Create your first orb' })).toBeInTheDocument()
  })

  it('shows server errors in words', async () => {
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockRejectedValue(new TypeError('Failed to fetch')))

    renderPage(<OrbsPage />)

    expect((await screen.findAllByRole('alert'))[0]).toHaveTextContent('Cannot reach the Orbs server')
  })

  it('imports an orb from a template file', async () => {
    const importOrb = vi.fn<(init?: RequestInit) => unknown>(() => ({
      orb: { ...pip, id: 'o2' },
      schedules: 0,
    }))
    mockApi({
      'GET /api/orbs': [pip],
      'GET /api/providers': [{ id: 'p1' }],
      'GET /api/context-sources': [],
      'POST /api/orbs/import': importOrb,
    })
    const user = userEvent.setup()
    renderPage(<OrbsPage />)

    const template = { format: 'orbs-template', version: 1, orb: { name: 'Chef' } }
    await user.upload(
      await screen.findByLabelText('Import orb'),
      new File([JSON.stringify(template)], 'chef.orb.json'),
    )

    await vi.waitFor(() => expect(importOrb).toHaveBeenCalled())
    expect(JSON.parse(String(importOrb.mock.calls[0]![0]?.body))).toEqual(template)
  })

  it('says so when a file is not a template', async () => {
    mockApi({ 'GET /api/orbs': [pip], 'GET /api/providers': [{ id: 'p1' }], 'GET /api/context-sources': [] })
    const user = userEvent.setup()
    renderPage(<OrbsPage />)

    await user.upload(await screen.findByLabelText('Import orb'), new File(['not json'], 'notes.json'))

    expect(await screen.findByText('This file is not an orb template.')).toBeInTheDocument()
  })
})
