import type { ReactElement } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router'
import { vi } from 'vitest'
import { SettingsProvider } from '@/app/SettingsContext'

/** Renders a page inside the app's providers, at a given URL. */
export function renderRoute(routes: RouteObject[], url = '/') {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const router = createMemoryRouter(routes, { initialEntries: [url] })
  const utils = render(
    <SettingsProvider>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </SettingsProvider>,
  )
  return { ...utils, router, client }
}

export const renderPage = (element: ReactElement, path = '/', url = path) =>
  renderRoute([{ path, element }], url)

type Handler = (init?: RequestInit) => unknown

/** Fakes the API: maps "METHOD /path" to a JSON body (or a function returning one). */
export function mockApi(routes: Record<string, unknown | Handler>) {
  const fetchMock = vi.fn<typeof fetch>(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input), 'http://localhost')
    const key = `${init?.method ?? 'GET'} ${url.pathname}`
    if (!(key in routes))
      return new Response(JSON.stringify({ title: `No mock for ${key}` }), { status: 404 })
    const value = routes[key]
    const body = typeof value === 'function' ? (value as Handler)(init) : value
    return new Response(JSON.stringify(body), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}
