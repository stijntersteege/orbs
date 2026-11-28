import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { mockApi, renderPage } from '@/test/render'
import { ContextPage } from './ContextPage'

describe('ContextPage uploads', () => {
  it('uploads a file as multipart form data', async () => {
    const fetchMock = mockApi({
      'GET /api/context-sources': [],
      'GET /api/orbs': [],
      'GET /api/context-sources/kinds': [
        { kind: 'file', displayName: 'File', description: 'Upload', usesLocation: false, usesInput: false },
      ],
      'POST /api/context-sources/files': { id: 's1', name: 'plan' },
    })
    const user = userEvent.setup({ applyAccept: false })
    renderPage(<ContextPage />)

    await user.click(await screen.findByRole('button', { name: /Add a source/ }))
    await user.upload(
      screen.getByLabelText('File'),
      new File(['# Plan'], 'plan.md', { type: 'text/markdown' }),
    )
    await user.click(screen.getByRole('button', { name: 'Upload' }))

    const isUpload = (c: Parameters<typeof fetch>) => String(c[0]).endsWith('/api/context-sources/files')
    await waitFor(() => expect(fetchMock.mock.calls.some(isUpload)).toBe(true))
    const call = fetchMock.mock.calls.find(isUpload)!
    const body = call[1]!.body as FormData
    expect((body.get('file') as File).name).toBe('plan.md')
    expect((call[1]!.headers as Record<string, string>)['Content-Type']).toBeUndefined()
  })
})
