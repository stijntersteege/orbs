import { useState, type FormEvent, type ReactNode } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { usersApi } from '@/api/endpoints'
import { keys, useAccount, useUsers } from '@/api/queries'
import { Button, Card, Chip, ErrorNotice, PageHeader, Spinner, Switch, TextField } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import type { User } from '@/api/types'
import shared from '../shared.module.css'
import styles from './account.module.css'

/** Admins create and remove accounts. Each account has its own orbs, models and chats. */
export function UsersPage() {
  const { data: account } = useAccount()
  const { data: users, error } = useUsers()
  const client = useQueryClient()
  const refresh = () => client.invalidateQueries({ queryKey: keys.users })
  const [email, setEmail] = useState('')
  const [name, setName] = useState('')
  const [password, setPassword] = useState('')
  const [admin, setAdmin] = useState(false)

  const create = useMutation({
    mutationFn: () => usersApi.create({ email, displayName: name || undefined, password, isAdmin: admin }),
    onSuccess: () => {
      setEmail('')
      setName('')
      setPassword('')
      setAdmin(false)
      void refresh()
    },
  })
  const remove = useMutation({ mutationFn: usersApi.remove, onSuccess: refresh })

  if (account && !account.user.isAdmin) {
    return <ErrorNotice error={new Error('Only admins can manage accounts.')} />
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    create.mutate()
  }

  return (
    <>
      <PageHeader
        title="Accounts"
        subtitle="Everyone on this server gets their own orbs, models and chats."
      />
      <div className={shared.stack} style={{ maxWidth: 760 }}>
        <Card>
          {error ? (
            <ErrorNotice error={error} />
          ) : !users ? (
            <Spinner />
          ) : (
            <ul aria-label="Accounts" className={shared.list}>
              {users.map((u) => (
                <UserRow key={u.id} user={u} isMe={u.id === account?.user.id} onChange={refresh}>
                  {u.id !== account?.user.id && (
                    <Button
                      small
                      variant="ghost"
                      aria-label={`Delete ${u.email}`}
                      onClick={() => {
                        if (globalThis.confirm?.(`Delete ${u.email} and everything they own?`) ?? true)
                          remove.mutate(u.id)
                      }}
                    >
                      <Icon name="trash" />
                    </Button>
                  )}
                </UserRow>
              ))}
            </ul>
          )}
          <ErrorNotice error={remove.error} />
        </Card>

        <Card>
          <form className={shared.stack} onSubmit={submit} aria-label="New account">
            <h2>New account</h2>
            <TextField
              label="Email"
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
            <TextField
              label="Name"
              value={name}
              placeholder="Optional"
              onChange={(e) => setName(e.target.value)}
            />
            <TextField
              label="First password"
              type="password"
              autoComplete="new-password"
              minLength={10}
              required
              hint="Share it safely; they can change it and add a passkey after signing in."
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            <Switch label="Admin" description="Can manage accounts." checked={admin} onChange={setAdmin} />
            <ErrorNotice error={create.error} />
            <div className={shared.actions}>
              <Button type="submit" variant="primary" disabled={create.isPending}>
                Create account
              </Button>
            </div>
          </form>
        </Card>
      </div>
    </>
  )
}

/** One account: its name, email and admin badge, or a form to change them. */
function UserRow({
  user,
  isMe,
  onChange,
  children,
}: {
  user: User
  isMe: boolean
  onChange: () => void
  children: ReactNode
}) {
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(user.displayName)
  const [admin, setAdmin] = useState(user.isAdmin)
  const [password, setPassword] = useState('')
  const save = useMutation({
    mutationFn: () =>
      usersApi.update(user.id, { displayName: name, isAdmin: admin, newPassword: password || undefined }),
    onSuccess: () => {
      setEditing(false)
      setPassword('')
      onChange()
    },
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    save.mutate()
  }

  if (editing)
    return (
      <li className={shared.editBox}>
        <form className={shared.stack} onSubmit={submit} aria-label={`Edit ${user.email}`}>
          <strong>{user.email}</strong>
          <TextField label="Name" required value={name} onChange={(e) => setName(e.target.value)} />
          {!isMe && (
            <Switch label="Admin" description="Can manage accounts." checked={admin} onChange={setAdmin} />
          )}
          <TextField
            label="New password"
            type="password"
            autoComplete="new-password"
            minLength={10}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            hint={
              isMe
                ? 'Change your own password on the Account page.'
                : 'Optional. Signs them out on every device.'
            }
            disabled={isMe}
          />
          <ErrorNotice error={save.error} />
          <div className={shared.actions}>
            <Button type="submit" variant="primary" disabled={save.isPending || !name.trim()}>
              Save changes
            </Button>
            <Button variant="ghost" onClick={() => setEditing(false)}>
              Cancel
            </Button>
          </div>
        </form>
      </li>
    )

  return (
    <li className={styles.item}>
      <span className={styles.itemText}>
        <span>
          {user.displayName} {user.isAdmin && <Chip>Admin</Chip>}
        </span>
        <span>{user.email}</span>
      </span>
      <Button small onClick={() => setEditing(true)}>
        Edit
      </Button>
      {children}
    </li>
  )
}
