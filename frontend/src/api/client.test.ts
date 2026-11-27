import { saveSettings, DEFAULT_SETTINGS } from '@/lib/settings'
import { apiBase, ApiError, http, SIGNED_OUT_EVENT } from './client'

describe('api client', () => {
  it('uses the page origin unless a server is configured', () => {
    expect(apiBase()).toBe('')
    saveSettings({ ...DEFAULT_SETTINGS, serverUrl: 'https://orbs.example.com/' })
    expect(apiBase()).toBe('https://orbs.example.com')
  })

  it('sends the API token when one is set', async () => {
    saveSettings({ ...DEFAULT_SETTINGS, apiToken: 'secret' })
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response('[]', { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)

    await http.get('/api/orbs')

    const init = fetchMock.mock.calls[0]![1] as RequestInit
    expect((init.headers as Record<string, string>).Authorization).toBe('Bearer secret')
  })

  it('turns problem details into readable errors', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockResolvedValue(
          new Response(JSON.stringify({ title: 'Bad', detail: 'Name is required.' }), { status: 400 }),
        ),
    )

    await expect(http.post('/api/orbs', {})).rejects.toEqual(new ApiError(400, 'Name is required.'))
  })

  it('turns a 401 into a sign-in prompt and tells the app', async () => {
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockResolvedValue(new Response('', { status: 401 })))
    const signedOut = vi.fn<() => void>()
    globalThis.addEventListener(SIGNED_OUT_EVENT, signedOut)

    await expect(http.get('/api/orbs')).rejects.toThrow(/Sign in/)
    expect(signedOut).toHaveBeenCalledOnce()
    globalThis.removeEventListener(SIGNED_OUT_EVENT, signedOut)
  })

  it('keeps the reason when signing in itself fails', async () => {
    const body = JSON.stringify({ detail: 'That email and password do not match.' })
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockResolvedValue(new Response(body, { status: 401 })))

    await expect(http.post('/api/auth/sign-in', {})).rejects.toThrow(/do not match/)
  })
})
