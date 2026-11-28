import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { mockApi, renderPage } from '@/test/render'
import { SkillsPage } from './SkillsPage'

const review = {
  id: 'k1',
  name: 'weekly-review',
  description: 'Sum up the week',
  instructions: '1. Read the calendar.',
  createdAt: '',
  updatedAt: '',
}

describe('SkillsPage', () => {
  it('lists skills and adds a new one', async () => {
    const create = vi.fn<(init?: RequestInit) => unknown>((init) => ({
      ...review,
      ...(JSON.parse(String(init?.body)) as object),
      id: 'k2',
    }))
    mockApi({ 'GET /api/skills': [review], 'POST /api/skills': create })
    const user = userEvent.setup()
    renderPage(<SkillsPage />)

    expect(await screen.findByText('weekly-review')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Name'), 'trip-plan')
    await user.type(screen.getByLabelText('When to use it'), 'Plan a trip')
    await user.type(screen.getByLabelText('Steps'), 'Search flights')
    await user.click(screen.getByRole('button', { name: 'Add skill' }))

    expect(JSON.parse(String(create.mock.calls[0]![0]?.body))).toEqual({
      name: 'trip-plan',
      description: 'Plan a trip',
      instructions: 'Search flights',
    })
  })

  it('edits a skill in the form', async () => {
    const update = vi.fn<(init?: RequestInit) => unknown>(() => review)
    mockApi({ 'GET /api/skills': [review], 'PUT /api/skills/k1': update })
    const user = userEvent.setup()
    renderPage(<SkillsPage />)

    await user.click(await screen.findByRole('button', { name: 'Edit' }))
    expect(screen.getByLabelText('Steps')).toHaveValue('1. Read the calendar.')
    await user.click(screen.getByRole('button', { name: 'Save skill' }))

    expect(update).toHaveBeenCalled()
  })

  it('imports a SKILL.md file', async () => {
    const importSkill = vi.fn<(init?: RequestInit) => unknown>(() => review)
    mockApi({ 'GET /api/skills': [], 'POST /api/skills/import': importSkill })
    const user = userEvent.setup()
    renderPage(<SkillsPage />)

    const text = '---\nname: weekly-review\ndescription: Sum up the week\n---\nSteps'
    await user.upload(await screen.findByLabelText('SKILL.md file'), new File([text], 'SKILL.md'))

    expect(await screen.findByText('Imported weekly-review.')).toBeInTheDocument()
    expect(JSON.parse(String(importSkill.mock.calls[0]![0]?.body))).toEqual({ markdown: text })
  })
})
