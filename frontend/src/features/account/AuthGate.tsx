import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { apiBase, ApiError, SIGNED_OUT_EVENT } from '@/api/client'
import { authApi } from '@/api/endpoints'
import { keys, useAccount, useAuthStatus } from '@/api/queries'
import type { SignInResult } from '@/api/types'
import { useSettings } from '@/app/SettingsContext'
import { Orb } from '@/components/orb/Orb'
import { Button, Card, ErrorNotice, Spinner, TextField } from '@/components/ui'
import { getPasskey, passkeysSupported } from '@/lib/webauthn'
import styles from './account.module.css'

const SIGN_IN_ERRORS: Record<string, string> = {
  oidc: 'Single sign-on did not finish. Try again.',
  'no-account': 'There is no Orbs account for that login yet. Ask an admin to create one.',
}

/** Shows the app to signed-in people, and the sign-in or first-run screen to everyone else. */
export function AuthGate({ children }: { children: ReactNode }) {
  const client = useQueryClient()
  const account = useAccount()

  useEffect(() => {
    const onSignedOut = () => void client.invalidateQueries({ queryKey: keys.account })
    globalThis.addEventListener(SIGNED_OUT_EVENT, onSignedOut)
    return () => globalThis.removeEventListener(SIGNED_OUT_EVENT, onSignedOut)
  }, [client])

  if (account.isPending) {
    return (
      <div className={styles.center}>
        <Spinner label="Loading your account" />
      </div>
    )
  }
  if (account.error instanceof ApiError && account.error.status === 401) return <SignInScreen />
  if (account.error) {
    return (
      <div className={styles.center}>
        <ErrorNotice error={account.error} />
      </div>
    )
  }
  return <>{children}</>
}

/** Stores what a remote server needs, then reloads everything as the new user. */
export function useSignedIn() {
  const client = useQueryClient()
  const { update } = useSettings()
  return (result: SignInResult) => {
    // A server on this page's own address uses the HttpOnly cookie; only a remote one needs the token.
    if (apiBase()) update({ sessionToken: result.token })
    void client.resetQueries()
  }
}

function SignInScreen() {
  const status = useAuthStatus()
  const signedIn = useSignedIn()
  const [email, setEmail] = useState('')
  const [name, setName] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<unknown>(() => {
    const code = new URLSearchParams(globalThis.location?.search).get('signInError')
    return code && SIGN_IN_ERRORS[code] ? new Error(SIGN_IN_ERRORS[code]) : null
  })
  const [busy, setBusy] = useState(false)

  if (status.isPending) {
    return (
      <div className={styles.center}>
        <Spinner />
      </div>
    )
  }
  const setup = status.data?.setupRequired ?? false

  const run = async (action: () => Promise<SignInResult>) => {
    setBusy(true)
    setError(null)
    try {
      signedIn(await action())
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    void run(() =>
      setup
        ? authApi.setup({ email, displayName: name || undefined, password })
        : authApi.signIn({ email, password }),
    )
  }

  const withPasskey = () =>
    run(async () => {
      const { ceremonyId, options } = await authApi.passkeyOptions()
      return authApi.passkeySignIn({ ceremonyId, response: await getPasskey(options) })
    })

  return (
    <main className={styles.center}>
      <Card className={styles.signIn}>
        <div className={styles.brand}>
          <Orb size={56} appearance={{ eyes: setup ? 'wide' : 'happy', accessory: 'antenna' }} />
          <h1>{setup ? 'Welcome to Orbs' : 'Sign in to Orbs'}</h1>
          <p className={styles.muted}>
            {setup
              ? 'Create the first account. It becomes the admin and keeps everything already on this server.'
              : 'Your orbs, models and chats are waiting.'}
          </p>
        </div>

        <form className={styles.form} onSubmit={submit} aria-label={setup ? 'Create account' : 'Sign in'}>
          <TextField
            label="Email"
            type="email"
            autoComplete={setup ? 'email' : 'username webauthn'}
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
          {setup && (
            <TextField
              label="Your name"
              autoComplete="name"
              value={name}
              placeholder="Optional"
              onChange={(e) => setName(e.target.value)}
            />
          )}
          <TextField
            label="Password"
            type="password"
            autoComplete={setup ? 'new-password' : 'current-password'}
            required
            minLength={setup ? 10 : undefined}
            hint={setup ? 'At least 10 characters. You can add a passkey afterwards.' : undefined}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          <ErrorNotice error={error} />
          <Button type="submit" variant="primary" disabled={busy}>
            {setup ? 'Create account' : 'Sign in'}
          </Button>
        </form>

        {!setup && (passkeysSupported() || status.data?.oidc) && (
          <div className={styles.alternatives}>
            <span className={styles.or}>or</span>
            {passkeysSupported() && (
              <Button onClick={() => void withPasskey()} disabled={busy}>
                Sign in with a passkey
              </Button>
            )}
            {status.data?.oidc && (
              <a className={styles.linkButton} href={`${apiBase()}${authApi.oidcUrl('/')}`}>
                Continue with {status.data.oidc.displayName}
              </a>
            )}
          </div>
        )}
      </Card>
    </main>
  )
}
