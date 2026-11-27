import { useEffect, useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { notificationsApi } from '@/api/endpoints'
import { keys, useNotificationSettings } from '@/api/queries'
import type { NotificationSettings } from '@/api/types'
import { Button, Card, ErrorNotice, Spinner, TextField } from '@/components/ui'
import { currentPushEndpoint, pushSupported, subscribeToPush } from '@/lib/push'
import shared from '../shared.module.css'

const localTimeZone = () => new Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'

/** Notifications on this device, and quiet hours when they wait. */
export function NotificationsCard() {
  const { data: settings, error } = useNotificationSettings()
  const client = useQueryClient()
  const refresh = () => client.invalidateQueries({ queryKey: keys.notifications })
  const [enabledHere, setEnabledHere] = useState(false)

  useEffect(() => {
    void currentPushEndpoint().then((endpoint) => setEnabledHere(!!endpoint))
  }, [])

  const enable = useMutation({
    mutationFn: async () => {
      const { publicKey } = await notificationsApi.key()
      const subscription = await subscribeToPush(publicKey)
      await notificationsApi.subscribe({
        endpoint: subscription.endpoint!,
        keys: { p256dh: subscription.keys!.p256dh!, auth: subscription.keys!.auth! },
      })
    },
    onSuccess: () => {
      setEnabledHere(true)
      void refresh()
    },
  })
  const test = useMutation({ mutationFn: notificationsApi.test })

  return (
    <Card>
      <div className={shared.stack}>
        <h2>Notifications</h2>
        <p className={shared.muted}>
          Orbs let you know when they did something on their own, such as a schedule, or need your approval.
          {settings && ` ${settings.devices} ${settings.devices === 1 ? 'device gets' : 'devices get'} them.`}
        </p>
        <div className={shared.actions}>
          {pushSupported() ? (
            <Button onClick={() => enable.mutate()} disabled={enable.isPending || enabledHere}>
              {enabledHere ? 'On for this device' : 'Turn on for this device'}
            </Button>
          ) : (
            <span className={shared.muted}>This browser cannot show notifications.</span>
          )}
          <Button variant="ghost" onClick={() => test.mutate()} disabled={test.isPending}>
            Send a test
          </Button>
          {test.data && <output className={shared.muted}>Sent to {test.data.delivered} device(s).</output>}
        </div>
        <ErrorNotice error={error ?? enable.error ?? test.error} />
        {settings ? <QuietHoursForm settings={settings} onSaved={refresh} /> : !error && <Spinner />}
      </div>
    </Card>
  )
}

function QuietHoursForm({ settings, onSaved }: { settings: NotificationSettings; onSaved: () => void }) {
  const [from, setFrom] = useState(settings.quietFrom ?? '')
  const [until, setUntil] = useState(settings.quietUntil ?? '')
  const [timeZone, setTimeZone] = useState(settings.quietFrom ? settings.timeZone : localTimeZone)
  const save = useMutation({
    mutationFn: () =>
      notificationsApi.saveSettings({
        quietFrom: from || undefined,
        quietUntil: until || undefined,
        timeZone,
      }),
    onSuccess: onSaved,
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    save.mutate()
  }

  return (
    <form className={shared.stack} onSubmit={submit} aria-label="Quiet hours">
      <h3>Quiet hours</h3>
      <p className={shared.muted}>
        Notifications wait until quiet hours end. Leave empty to always get them.
      </p>
      <div className={shared.grid2}>
        <TextField label="From" type="time" value={from} onChange={(e) => setFrom(e.target.value)} />
        <TextField label="Until" type="time" value={until} onChange={(e) => setUntil(e.target.value)} />
      </div>
      <TextField label="Time zone" value={timeZone} onChange={(e) => setTimeZone(e.target.value)} />
      <ErrorNotice error={save.error} />
      <div className={shared.actions}>
        <Button type="submit" variant="primary" disabled={save.isPending}>
          Save quiet hours
        </Button>
        {save.isSuccess && <output className={shared.muted}>Saved.</output>}
      </div>
    </form>
  )
}
