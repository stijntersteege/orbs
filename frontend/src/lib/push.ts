// Web Push in the browser: register the service worker and subscribe with the server's VAPID key.
import { fromBase64Url } from './webauthn'

export function pushSupported(): boolean {
  return 'serviceWorker' in navigator && 'PushManager' in globalThis && 'Notification' in globalThis
}

/** Registers /sw.js once. Called at start-up in normal builds. */
export async function registerServiceWorker(): Promise<ServiceWorkerRegistration | undefined> {
  if (!('serviceWorker' in navigator)) return undefined
  return navigator.serviceWorker.register('/sw.js')
}

/** Asks for permission and subscribes this browser. Returns the subscription to send to the server. */
export async function subscribeToPush(publicKey: string): Promise<PushSubscriptionJSON> {
  const permission = await Notification.requestPermission()
  if (permission !== 'granted')
    throw new Error('Notifications are blocked for this site. Allow them in your browser settings.')
  const registration = (await navigator.serviceWorker.getRegistration()) ?? (await registerServiceWorker())
  if (!registration) throw new Error('This browser cannot show notifications.')
  await navigator.serviceWorker.ready
  const existing = await registration.pushManager.getSubscription()
  const subscription =
    existing ??
    (await registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: fromBase64Url(publicKey),
    }))
  return subscription.toJSON()
}

export async function currentPushEndpoint(): Promise<string | undefined> {
  if (!pushSupported()) return undefined
  const registration = await navigator.serviceWorker.getRegistration()
  return (await registration?.pushManager.getSubscription())?.endpoint
}
