/**
 * Reads a server-sent events stream from a fetch response.
 * EventSource only supports GET, and sending a chat message is a POST, so we parse by hand.
 */
export async function* readServerSentEvents<T>(body: ReadableStream<Uint8Array>): AsyncGenerator<T> {
  const reader = body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  try {
    while (true) {
      const { value, done } = await reader.read()
      if (done) break
      buffer += decoder.decode(value, { stream: true }).replaceAll('\r\n', '\n')
      let boundary = buffer.indexOf('\n\n')
      while (boundary !== -1) {
        const event = parseEvent(buffer.slice(0, boundary))
        buffer = buffer.slice(boundary + 2)
        if (event !== undefined) yield event as T
        boundary = buffer.indexOf('\n\n')
      }
    }
    const last = parseEvent(buffer)
    if (last !== undefined) yield last as T
  } finally {
    reader.releaseLock()
  }
}

/** Parses one event block. Only `data:` lines matter here; they hold JSON. */
export function parseEvent(block: string): unknown {
  const data = block
    .split('\n')
    .filter((line) => line.startsWith('data:'))
    .map((line) => line.slice(5).trimStart())
    .join('\n')
  return data ? JSON.parse(data) : undefined
}
