import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { accountApi, authApi } from '@/api/endpoints'
import { keys, useAccount } from '@/api/queries'
import { useSettings } from '@/app/SettingsContext'
import { Button, Card, Chip, ErrorNotice, PageHeader, Spinner, TextField } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import { createPasskey, passkeysSupported } from '@/lib/webauthn'
import shared from '../shared.module.css'
import styles from './account.module.css'
import { NotificationsCard } from './NotificationsCard'

const date = (iso: string) => new Date(iso).toLocaleDateString(undefined, { dateStyle: 'medium' })

/** The signed-in person: name, password, passkeys and signed-in devices. */
export function AccountPage() {
  const { data: account, error } = useAccount()
  const client = useQueryClient()
  const { update } = useSettings()
  const refresh = () => client.invalidateQueries({ queryKey: keys.account })

  const signOut = useMutation({
    mutationFn: authApi.signOut,
    onSuccess: () => {
      update({ sessionToken: '' })
      void client.resetQueries()
    },
  })

  if (error) return <ErrorNotice error={error} />
  if (!account) return <Spinner />

  return (
    <>
      <PageHeader
        title="Account"
        subtitle={account.user.email}
        actions={
          <Button onClick={() => signOut.mutate()} disabled={signOut.isPending}>
            Sign out
          </Button>
        }
      />
      <div className={shared.stack} style={{ maxWidth: 760 }}>
        <Profile name={account.user.displayName} onSaved={refresh} />
        <Password hasPassword={account.user.hasPassword} />
        <Passkeys passkeys={account.passkeys} onChange={refresh} />
        <NotificationsCard />
        <Card>
          <div className={shared.stack}>
            <h2>Signed-in devices</h2>
            <div>
              {account.sessions.map((s) => (
                <div key={s.id} className={styles.item}>
                  <span className={styles.itemText}>
                    <span>
                      {s.description || 'Unknown device'} {s.current && <Chip tone="ok">This device</Chip>}
                    </span>
                    <span>Last used {date(s.lastSeenAt)}</span>
                  </span>
                  {!s.current && (
                    <Button
                      small
                      onClick={() => void accountApi.revokeSession(s.id).then(refresh)}
                      aria-label={`Sign out ${s.description || 'device'}`}
                    >
                      Sign out
                    </Button>
                  )}
                </div>
              ))}
            </div>
          </div>
        </Card>
      </div>
    </>
  )
}

function Profile({ name, onSaved }: { name: string; onSaved: () => void }) {
  const [value, setValue] = useState(name)
  const save = useMutation({ mutationFn: () => accountApi.update(value), onSuccess: onSaved })
  return (
    <Card>
      <form
        className={shared.stack}
        aria-label="Profile"
        onSubmit={(e) => {
          e.preventDefault()
          save.mutate()
        }}
      >
        <h2>Profile</h2>
        <TextField label="Display name" value={value} onChange={(e) => setValue(e.target.value)} required />
        <ErrorNotice error={save.error} />
        <div className={shared.actions}>
          <Button type="submit" variant="primary" disabled={save.isPending || value.trim() === name}>
            Save name
          </Button>
        </div>
      </form>
    </Card>
  )
}

function Password({ hasPassword }: { hasPassword: boolean }) {
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const save = useMutation({
    mutationFn: () => accountApi.changePassword({ currentPassword: current || undefined, newPassword: next }),
    onSuccess: () => {
      setCurrent('')
      setNext('')
    },
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    save.mutate()
  }
  return (
    <Card>
      <form className={shared.stack} aria-label="Password" onSubmit={submit}>
        <h2>Password</h2>
        {hasPassword && (
          <TextField
            label="Current password"
            type="password"
            autoComplete="current-password"
            value={current}
            onChange={(e) => setCurrent(e.target.value)}
            required
          />
        )}
        <TextField
          label={hasPassword ? 'New password' : 'Password'}
          type="password"
          autoComplete="new-password"
          minLength={10}
          hint="At least 10 characters."
          value={next}
          onChange={(e) => setNext(e.target.value)}
          required
        />
        <ErrorNotice error={save.error} />
        <div className={shared.actions}>
          <Button type="submit" variant="primary" disabled={save.isPending}>
            {hasPassword ? 'Change password' : 'Set password'}
          </Button>
          {save.isSuccess && <output className={shared.muted}>Saved.</output>}
        </div>
      </form>
    </Card>
  )
}

function Passkeys({
  passkeys,
  onChange,
}: {
  passkeys: { id: string; name: string; createdAt: string; lastUsedAt?: string }[]
  onChange: () => void
}) {
  const [name, setName] = useState('')
  const add = useMutation({
    mutationFn: async () => {
      const { ceremonyId, options } = await accountApi.passkeyOptions()
      return accountApi.addPasskey({
        ceremonyId,
        response: await createPasskey(options),
        name: name || undefined,
      })
    },
    onSuccess: () => {
      setName('')
      onChange()
    },
  })
  const remove = useMutation({ mutationFn: accountApi.removePasskey, onSuccess: onChange })

  return (
    <Card>
      <div className={shared.stack}>
        <h2>Passkeys</h2>
        <p className={shared.muted}>
          Sign in with your fingerprint, face or device PIN instead of a password. The secret part never
          leaves your device.
        </p>
        {passkeys.length > 0 && (
          <div>
            {passkeys.map((p) => (
              <div key={p.id} className={styles.item}>
                <Icon name="check" />
                <span className={styles.itemText}>
                  <span>{p.name}</span>
                  <span>
                    Added {date(p.createdAt)}
                    {p.lastUsedAt ? ` · last used ${date(p.lastUsedAt)}` : ''}
                  </span>
                </span>
                <Button
                  small
                  variant="ghost"
                  aria-label={`Remove ${p.name}`}
                  onClick={() => remove.mutate(p.id)}
                >
                  <Icon name="trash" />
                </Button>
              </div>
            ))}
          </div>
        )}
        {passkeysSupported() ? (
          <form
            className={shared.actions}
            aria-label="Add a passkey"
            onSubmit={(e) => {
              e.preventDefault()
              add.mutate()
            }}
          >
            <TextField
              label="Passkey name"
              placeholder="For example: Phone"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
            <Button type="submit" disabled={add.isPending} style={{ alignSelf: 'flex-end' }}>
              Add a passkey
            </Button>
          </form>
        ) : (
          <p className={shared.muted}>This browser does not support passkeys.</p>
        )}
        <ErrorNotice error={add.error ?? remove.error} />
      </div>
    </Card>
  )
}
