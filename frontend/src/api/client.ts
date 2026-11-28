import { loadSettings } from '@/lib/settings'
import type { ProblemDetails } from './types'

/** An API call failed. `message` is safe to show to people. */
export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/** The server the app talks to: a configured URL (mobile, remote) or the page's own origin. */
export function apiBase(): string {
  const configured = loadSettings().serverUrl.trim() || import.meta.env.VITE_API_URL || ''
  return configured.replace(/\/+$/, '')
}

/** Fired when the server says nobody is signed in, so the app can show the sign-in screen. */
export const SIGNED_OUT_EVENT = 'orbs:signed-out'

function headers(json: boolean): HeadersInit {
  const settings = loadSettings()
  const token = settings.apiToken.trim() || settings.sessionToken.trim()
  return {
    Accept: 'application/json',
    ...(json ? { 'Content-Type': 'application/json' } : {}),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  }
}

async function toError(response: Response, path: string): Promise<ApiError> {
  let message = `${response.status} ${response.statusText}`.trim()
  try {
    const problem = (await response.json()) as ProblemDetails
    message = problem.detail ?? problem.title ?? message
  } catch {
    // Not JSON: keep the status line.
  }
  if (response.status === 401 && !path.startsWith('/api/auth/')) {
    message = 'Sign in to continue.'
    globalThis.dispatchEvent?.(new Event(SIGNED_OUT_EVENT))
  }
  return new ApiError(response.status, message)
}

/** Low-level request. Prefer the typed helpers below. */
export async function request(path: string, init: RequestInit = {}): Promise<Response> {
  const json = init.body !== undefined
  let response: Response
  try {
    response = await fetch(`${apiBase()}${path}`, { ...init, headers: { ...headers(json), ...init.headers } })
  } catch {
    throw new ApiError(0, 'Cannot reach the Orbs server. Check the server address under Settings.')
  }
  if (!response.ok) throw await toError(response, path)
  return response
}

export const http = {
  async get<T>(path: string): Promise<T> {
    return (await request(path)).json() as Promise<T>
  },
  async post<T>(path: string, body?: unknown): Promise<T> {
    const response = await request(path, { method: 'POST', body: JSON.stringify(body ?? {}) })
    return response.json() as Promise<T>
  },
  async put<T>(path: string, body: unknown): Promise<T> {
    return (await request(path, { method: 'PUT', body: JSON.stringify(body) })).json() as Promise<T>
  },
  async delete(path: string): Promise<void> {
    await request(path, { method: 'DELETE' })
  },
}
