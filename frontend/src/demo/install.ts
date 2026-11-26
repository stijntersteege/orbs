import { DemoServer } from './server'

/**
 * Routes every `/api/...` fetch to the in-browser demo server; other requests pass through.
 * Only loaded in demo builds (`vite build --mode demo`), so normal builds never include it.
 */
export function installDemo(server = new DemoServer()): DemoServer {
  const realFetch = globalThis.fetch.bind(globalThis)

  globalThis.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
    const raw = input instanceof Request ? input.url : input.toString()
    const url = new URL(raw, globalThis.location?.href)
    if (!url.pathname.includes('/api/')) return realFetch(input, init)

    const request = new Request(url, init)
    const text = request.method === 'GET' || request.method === 'HEAD' ? '' : await request.text()
    return server.handle(request.method, url, text ? JSON.parse(text) : undefined, init?.signal ?? undefined)
  }

  return server
}
