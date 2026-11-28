import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Markdown from './MarkdownRenderer'

describe('Markdown', () => {
  it('shows headings, lists, tables and task lists', () => {
    render(
      <Markdown>
        {'## Plan\n\n- **Soup** on Friday\n- [x] Buy bread\n\n| Day | Meal |\n| --- | --- |\n| Fri | Soup |'}
      </Markdown>,
    )

    expect(screen.getByRole('heading', { level: 2, name: 'Plan' })).toBeInTheDocument()
    expect(screen.getByText('Soup', { selector: 'strong' })).toBeInTheDocument()
    expect(screen.getByRole('checkbox')).toBeChecked()
    expect(screen.getByRole('table')).toHaveTextContent('Fri')
  })

  it('shows code in a block with its language and a copy button', async () => {
    const writeText = vi.fn<(text: string) => Promise<void>>(() => Promise.resolve())
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true })
    render(<Markdown>{'```ts\nconst answer = 42\n```'}</Markdown>)

    expect(screen.getByText('ts')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Copy' }))

    expect(writeText).toHaveBeenCalledWith(expect.stringContaining('const answer = 42'))
    expect(screen.getByRole('button', { name: 'Copied' })).toBeInTheDocument()
    Reflect.deleteProperty(navigator, 'clipboard')
  })

  it('opens links in a new tab and drops unsafe ones', () => {
    render(<Markdown>{'[docs](https://orbs.example/docs) and [bad](javascript:alert(1))'}</Markdown>)

    const docs = screen.getByRole('link', { name: 'docs' })
    expect(docs).toHaveAttribute('target', '_blank')
    expect(docs).toHaveAttribute('rel', expect.stringContaining('noopener'))
    expect(screen.getByText('bad').closest('a')?.getAttribute('href') ?? '').not.toContain('javascript')
  })

  it('never runs HTML from the text and does not load pictures', () => {
    const { container } = render(
      <Markdown>
        {'<script>alert(1)</script><b>bold?</b>\n\n![tracker](https://evil.example/pixel.png)'}
      </Markdown>,
    )

    expect(container.querySelector('script')).toBeNull()
    expect(container.querySelector('b')).toBeNull()
    expect(container.querySelector('img')).toBeNull()
    expect(screen.getByRole('link', { name: 'tracker' })).toHaveAttribute(
      'href',
      'https://evil.example/pixel.png',
    )
  })
})
