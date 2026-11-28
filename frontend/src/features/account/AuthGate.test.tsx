import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderPage } from '@/test/render'
import { AuthGate } from './AuthGate'

const user = {
  id: 'u1',
  email: 'sam@example.com',
  displayName: 'Sam',
  isAdmin: true,
  hasPassword: true,
  createdAt: '',
}
const account = { user, passkeys: [], sessions: [], hasExternalLogin: false }

function stubFetch(routes: Record<string, () => Response>) {
  const fetchMock = vi.fn<typeof fetch>(async (input, init) => {
    const key = `${init?.method ?? 'GET'} ${new URL(String(input), 'http://localhost').pathname}`
    return routes[key]?.() ?? new Response('{}', { status: 404 })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status })

describe('AuthGate', () => {
  it('asks for the first account on a fresh server', async () => {
    stubFetch({
      'GET /api/account': () => new Response('', { status: 401 }),
      'GET /api/auth/status': () => json({ setupRequired: true, passwordSignIn: true, passkeys: true }),
    })
    renderPage(<AuthGate>secret app</AuthGate>)

    expect(await screen.findByRole('heading', { name: 'Welcome to Orbs' })).toBeInTheDocument()
    expect(screen.getByLabelText('Your name')).toBeInTheDocument()
    expect(screen.queryByText('secret app')).not.toBeInTheDocument()
  })

  it('signs in with a password and then shows the app', async () => {
    let signedIn = false
    const fetchMock = stubFetch({
      'GET /api/account': () => (signedIn ? json(account) : new Response('', { status: 401 })),
      'GET /api/auth/status': () =>
        json({
          setupRequired: false,
          passwordSignIn: true,
          passkeys: true,
          oidc: { displayName: 'Authentik' },
        }),
      'POST /api/auth/sign-in': () => {
        signedIn = true
        return json({ token: 't', expiresAt: '', user })
      },
    })
    const u = userEvent.setup()
    renderPage(<AuthGate>secret app</AuthGate>)

    expect(await screen.findByRole('link', { name: 'Continue with Authentik' })).toBeInTheDocument()
    await u.type(screen.getByLabelText('Email'), 'sam@example.com')
    await u.type(screen.getByLabelText('Password'), 'correct horse battery')
    await u.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('secret app')).toBeInTheDocument()
    const body = JSON.parse(
      String(fetchMock.mock.calls.find((c) => String(c[0]).endsWith('/sign-in'))![1]!.body),
    )
    expect(body).toEqual({ email: 'sam@example.com', password: 'correct horse battery' })
  })

  it('shows why signing in failed', async () => {
    stubFetch({
      'GET /api/account': () => new Response('', { status: 401 }),
      'GET /api/auth/status': () => json({ setupRequired: false, passwordSignIn: true, passkeys: true }),
      'POST /api/auth/sign-in': () => json({ detail: 'That email and password do not match.' }, 401),
    })
    const u = userEvent.setup()
    renderPage(<AuthGate>secret app</AuthGate>)

    await u.type(await screen.findByLabelText('Email'), 'sam@example.com')
    await u.type(screen.getByLabelText('Password'), 'nope nope nope')
    await u.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('do not match')
  })
})
