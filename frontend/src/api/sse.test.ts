import { parseEvent, readServerSentEvents } from './sse'

function streamOf(...chunks: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder()
  return new ReadableStream({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(encoder.encode(chunk))
      controller.close()
    },
  })
}

async function collect<T>(gen: AsyncGenerator<T>): Promise<T[]> {
  const out: T[] = []
  for await (const item of gen) out.push(item)
  return out
}

describe('parseEvent', () => {
  it('reads the JSON in data lines and ignores the rest', () => {
    expect(parseEvent('event: delta\ndata: {"type":"delta","text":"Hi"}')).toEqual({
      type: 'delta',
      text: 'Hi',
    })
  })

  it('returns undefined for comments and empty blocks', () => {
    expect(parseEvent(': keep-alive')).toBeUndefined()
    expect(parseEvent('')).toBeUndefined()
  })
})

describe('readServerSentEvents', () => {
  it('yields events in order, even when split across chunks', async () => {
    const events = await collect(
      readServerSentEvents(
        streamOf(
          'event: start\ndata: {"type":"start","model":"m"}\n\nevent: del',
          'ta\ndata: {"type":"delta","text":"Hel',
          'lo"}\n\n',
        ),
      ),
    )
    expect(events).toEqual([
      { type: 'start', model: 'm' },
      { type: 'delta', text: 'Hello' },
    ])
  })

  it('handles CRLF line endings and a final event without a blank line', async () => {
    const events = await collect(readServerSentEvents(streamOf('data: {"a":1}\r\n\r\ndata: {"a":2}')))
    expect(events).toEqual([{ a: 1 }, { a: 2 }])
  })
})
