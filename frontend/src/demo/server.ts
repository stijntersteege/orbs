// An in-browser stand-in for the Orbs API, used by the static live demo.
// It mirrors the real endpoints closely enough for every screen to work, keeps data in
// localStorage, and answers chats like the Echo provider. Real model providers are not
// called: the demo has no server and must never send anyone's API key anywhere.

import type {
  ChatEvent,
  ContextKind,
  ChatSource,
  ContextSource,
  Conversation,
  Memory,
  Message,
  Orb,
  Provider,
  ProviderKind,
  SaveContextSourceRequest,
  SaveOrbRequest,
  SaveProviderRequest,
} from '@/api/types'
import { DEFAULT_APPEARANCE } from '@/api/types'
import { DEMO_STORAGE_KEY } from './mode'
import { seed, type DemoState } from './seed'

const STORAGE_KEY = DEMO_STORAGE_KEY
const ECHO_KIND = 'echo'
/** Same as PromptBudget.MaxContextCharacters on the server. */
const PROMPT_CONTEXT_CHARACTERS = 48_000

export const PROVIDER_KINDS: ProviderKind[] = [
  {
    kind: 'openai-compatible',
    displayName: 'OpenAI-compatible',
    description:
      'Any endpoint that speaks the OpenAI chat API: OpenAI, OpenRouter, Ollama, LM Studio, vLLM, LiteLLM.',
    requiresBaseUrl: true,
    requiresApiKey: false,
  },
  {
    kind: 'anthropic',
    displayName: 'Anthropic',
    description: 'Claude models through the Anthropic Messages API, with tool use.',
    requiresBaseUrl: false,
    requiresApiKey: true,
  },
  {
    kind: 'gemini',
    displayName: 'Google Gemini',
    description: 'Gemini models through the Google AI (Gemini) API, with function calling.',
    requiresBaseUrl: false,
    requiresApiKey: true,
  },
  {
    kind: ECHO_KIND,
    displayName: 'Echo (demo)',
    description: 'Repeats what you say. No key needed, handy for trying Orbs out.',
    requiresBaseUrl: false,
    requiresApiKey: false,
  },
]

export const CONTEXT_KINDS: ContextKind[] = [
  {
    kind: 'text',
    displayName: 'Note',
    description: 'Type or paste anything your orbs should know.',
    usesLocation: false,
    usesInput: true,
  },
  {
    kind: 'file',
    displayName: 'File',
    description: 'Upload a PDF, a Word or Markdown document, or an image.',
    usesLocation: false,
    usesInput: false,
  },
  {
    kind: 'url',
    displayName: 'Web page',
    description: 'Paste a link. Orbs reads the page and keeps its text in sync when you ask.',
    usesLocation: true,
    usesInput: false,
  },
]

const DEMO_TOOLS = [
  {
    name: 'calculator',
    displayName: 'Calculator',
    description: 'Evaluate an arithmetic expression.',
    risk: 'read',
  },
  {
    name: 'current_time',
    displayName: 'Current time',
    description: 'Get the current date and time.',
    risk: 'read',
  },
]

/** The demo's versions of the built-in tools. The calculator only does simple arithmetic. */
function runDemoTool(name: string, args: string, now: Date): { result: string; isError: boolean } {
  if (name === 'current_time') return { result: now.toUTCString(), isError: false }
  try {
    const expression = String((JSON.parse(args || '{}') as { expression?: string }).expression ?? '')
    if (!/^[\d\s+\-*/().]+$/.test(expression)) throw new Error('Only numbers and + - * / ( ) here.')
    const value = Function(`"use strict"; return (${expression})`)() as number
    if (!Number.isFinite(value)) throw new Error('The result is not a finite number.')
    return { result: String(value), isError: false }
  } catch (error) {
    return { result: error instanceof Error ? error.message : 'Failed.', isError: true }
  }
}

const OFFLINE_REPLY =
  'This is the live demo, so real models are switched off and nothing leaves your browser. ' +
  'Run Orbs on your own server to connect this provider, or pick the Echo provider to keep playing here.'

export interface DemoOptions {
  /** Pause between streamed words, in milliseconds. */
  wordDelay?: number
  now?: () => Date
}

export class HttpError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

/** The demo "server". Every method takes and returns plain API contracts. */
export class DemoServer {
  private state: DemoState
  private readonly wordDelay: number
  private readonly now: () => Date
  /** Tool calls waiting for an answer, per conversation. Kept in memory only. */
  private readonly pending = new Map<
    string,
    { approvalId: string; tool: { name: string; args: string }; model: string; provider: string }
  >()

  constructor(options: DemoOptions = {}) {
    this.wordDelay = options.wordDelay ?? 30
    this.now = options.now ?? (() => new Date())
    this.state = this.load()
  }

  /** Routes one request. Returns a Response like fetch would. */
  async handle(method: string, url: URL, body: unknown, signal?: AbortSignal): Promise<Response> {
    try {
      return await this.route(method.toUpperCase(), url, body, signal)
    } catch (error) {
      if (error instanceof HttpError) {
        return json({ title: error.message, detail: error.message, status: error.status }, error.status)
      }
      throw error
    }
  }

  reset(): void {
    this.state = seed(this.now())
    this.save()
  }

  private async route(method: string, url: URL, body: unknown, signal?: AbortSignal): Promise<Response> {
    const parts = url.pathname
      .replace(/^.*?\/api\//, '')
      .split('/')
      .filter(Boolean)
    const [resource, id, action] = parts
    const s = this.state

    switch (resource) {
      case 'orbs':
        if (!id) return method === 'GET' ? json(s.orbs) : json(this.saveOrb(undefined, body), 201)
        if (action === 'memories') {
          this.find(s.orbs, id, 'orb')
          if (method === 'GET') return json((s.memories ?? []).filter((m) => m.shared || m.orbId === id))
          return json(this.addMemory(id, body), 201)
        }
        if (method === 'GET') return json(this.find(s.orbs, id, 'orb'))
        if (method === 'PUT') return json(this.saveOrb(id, body))
        if (method === 'DELETE') return this.removeOrb(id)
        break

      case 'providers':
        if (id === 'kinds') return json(PROVIDER_KINDS)
        if (!id) return method === 'GET' ? json(s.providers) : json(this.saveProvider(undefined, body), 201)
        if (action === 'models') return json(this.models(id))
        if (method === 'PUT') return json(this.saveProvider(id, body))
        if (method === 'DELETE') return this.removeProvider(id)
        break

      case 'context-sources':
        if (id === 'kinds') return json(CONTEXT_KINDS)
        if (!id)
          return method === 'GET' ? json(s.contextSources) : json(this.saveSource(undefined, body), 201)
        if (id === 'files')
          throw new HttpError(
            400,
            'The live demo cannot read files. Self-host Orbs to upload documents and pictures.',
          )
        if (action === 'sync') return json(this.syncSource(id))
        if (method === 'PUT') return json(this.saveSource(id, body))
        if (method === 'DELETE') return this.removeSource(id)
        break

      case 'auth':
        if (id === 'status') return json({ setupRequired: false, passwordSignIn: true, passkeys: false })
        if (id === 'sign-out') return new Response(null, { status: 204 })
        throw new HttpError(400, 'The live demo has no sign-in: you are always the demo user.')

      case 'push':
        throw new HttpError(400, 'The live demo cannot send notifications. Self-host Orbs to get them.')

      case 'account':
        if (id === 'notifications') return json({ timeZone: 'UTC', devices: 0 })
        if (!id && method === 'GET') return json(this.account())
        if (!id && method === 'PUT') {
          this.state.user = {
            ...this.account().user,
            displayName: required((body as { displayName?: string }).displayName, 'name'),
          }
          this.save()
          return json(this.state.user)
        }
        throw new HttpError(400, 'Passwords and passkeys are not part of the live demo.')

      case 'voice':
        if (method === 'GET' && !id) return json({ canListen: false, canSpeak: false, voices: [] })
        throw new HttpError(
          400,
          'The live demo cannot listen or speak. Self-host Orbs and connect a speech model.',
        )

      case 'channels':
        if (method === 'GET' && id === 'kinds') return json([])
        if (method === 'GET' && !id) return json([])
        throw new HttpError(400, 'Channels need a server that keeps running. Self-host Orbs to use them.')

      case 'schedules':
        if (method === 'GET' && !id) return json([])
        throw new HttpError(400, 'Schedules need a server that keeps running. Self-host Orbs to use them.')

      case 'integrations':
        if (method === 'GET' && id === 'kinds') return json([])
        if (method === 'GET' && !id) return json([])
        throw new HttpError(400, 'The live demo cannot connect apps. Self-host Orbs to use them.')

      case 'mcp-servers':
        if (method === 'GET' && !id) return json([])
        throw new HttpError(400, 'The live demo cannot connect to MCP servers. Self-host Orbs to use them.')

      case 'tools':
        return json(DEMO_TOOLS)

      case 'jobs':
        // The demo has no server, so nothing runs in the background.
        return json([])

      case 'users':
        if (method === 'GET') return json([this.account().user])
        throw new HttpError(400, 'The live demo has a single account.')

      case 'memories':
        if (method === 'PUT') return json(this.editMemory(id!, body))
        if (method === 'DELETE') {
          this.state.memories = (this.state.memories ?? []).filter((m) => m.id !== id)
          this.save()
          return new Response(null, { status: 204 })
        }
        break

      case 'conversations': {
        if (!id) {
          if (method === 'GET') {
            const orbId = url.searchParams.get('orbId')
            return json(
              s.conversations
                .filter((c) => !orbId || c.orbId === orbId)
                .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))
                .map(({ messages: _messages, ...summary }) => summary),
            )
          }
          return json(this.startConversation(body), 201)
        }
        if (action === 'messages' && method === 'GET') return json(this.olderMessages(id, url))
        if (action === 'approvals') return this.decide(id, parts[3], body, signal)
        if (action === 'messages') return this.send(id, body, signal)
        if (method === 'GET') {
          const found = this.find(s.conversations, id, 'conversation')
          const waiting = this.pending.get(id)
          return json({
            ...found,
            pendingApproval: waiting
              ? {
                  id: waiting.approvalId,
                  callId: 'demo-call',
                  tool: waiting.tool.name,
                  arguments: waiting.tool.args,
                }
              : undefined,
          })
        }
        if (method === 'DELETE') {
          s.conversations = s.conversations.filter((c) => c.id !== id)
          this.save()
          return new Response(null, { status: 204 })
        }
        break
      }
    }
    throw new HttpError(404, 'Not found.')
  }

  // Account

  private account() {
    const user = this.state.user ?? {
      id: '00000000-0000-4000-8000-000000000001',
      email: 'demo@orbs.local',
      displayName: 'Demo',
      isAdmin: true,
      hasPassword: false,
      createdAt: this.timestamp(),
    }
    return {
      user,
      passkeys: [],
      sessions: [
        {
          id: 'demo',
          description: 'This browser',
          createdAt: user.createdAt,
          lastSeenAt: this.timestamp(),
          current: true,
        },
      ],
      hasExternalLogin: false,
    }
  }

  // Memory

  private addMemory(orbId: string, body: unknown): Memory {
    const now = this.timestamp()
    const shared = (body as { shared?: boolean })?.shared === true
    const memory: Memory = {
      id: newId(),
      orbId: shared ? undefined : orbId,
      shared,
      content: required((body as { content?: string })?.content, 'memory'),
      source: 'manual',
      createdAt: now,
      updatedAt: now,
    }
    this.state.memories = [memory, ...(this.state.memories ?? [])]
    this.save()
    return memory
  }

  private editMemory(id: string, body: unknown): Memory {
    const existing = this.find(this.state.memories ?? [], id, 'memory')
    const request = body as { content?: string; shared?: boolean; orbId?: string }
    const shared = request.shared ?? existing.shared
    const memory = {
      ...existing,
      content: required(request.content, 'memory'),
      shared,
      orbId: shared ? undefined : (request.orbId ?? existing.orbId),
      updatedAt: this.timestamp(),
    }
    this.state.memories = (this.state.memories ?? []).map((m) => (m.id === id ? memory : m))
    this.save()
    return memory
  }

  // Orbs

  private saveOrb(id: string | undefined, body: unknown): Orb {
    const request = body as SaveOrbRequest
    const name = required(request.name, 'name')
    const now = this.timestamp()
    const existing = id ? this.find(this.state.orbs, id, 'orb') : undefined
    const orb: Orb = {
      id: existing?.id ?? newId(),
      name,
      description: request.description?.trim() ?? '',
      instructions: request.instructions?.trim() ?? '',
      appearance: request.appearance ?? existing?.appearance ?? DEFAULT_APPEARANCE,
      model: request.model ?? undefined,
      contextSourceIds: request.contextSourceIds ?? existing?.contextSourceIds ?? [],
      toolPermissions: request.toolPermissions ?? existing?.toolPermissions,
      voice: request.voice?.trim() || undefined,
      createdAt: existing?.createdAt ?? now,
      updatedAt: now,
    }
    this.state.orbs = existing
      ? this.state.orbs.map((o) => (o.id === orb.id ? orb : o))
      : [...this.state.orbs, orb]
    this.save()
    return orb
  }

  private removeOrb(id: string): Response {
    this.find(this.state.orbs, id, 'orb')
    this.state.orbs = this.state.orbs.filter((o) => o.id !== id)
    this.state.conversations = this.state.conversations.filter((c) => c.orbId !== id)
    this.save()
    return new Response(null, { status: 204 })
  }

  // Providers

  private saveProvider(id: string | undefined, body: unknown): Provider {
    const request = body as SaveProviderRequest
    const name = required(request.name, 'name')
    if (!PROVIDER_KINDS.some((k) => k.kind === request.kind))
      throw new HttpError(400, 'Unknown provider kind.')
    const now = this.timestamp()
    const existing = id ? this.find(this.state.providers, id, 'provider') : undefined
    const provider: Provider = {
      id: existing?.id ?? newId(),
      name,
      kind: request.kind,
      baseUrl: request.baseUrl?.trim() || undefined,
      // The demo never stores keys; it only remembers that one was given.
      hasApiKey: request.apiKey === undefined ? (existing?.hasApiKey ?? false) : request.apiKey !== '',
      defaultModel: request.defaultModel?.trim() || (request.kind === ECHO_KIND ? 'echo-1' : undefined),
      isEnabled: request.isEnabled ?? existing?.isEnabled ?? true,
      embeddingModel: request.embeddingModel?.trim() || undefined,
      createdAt: existing?.createdAt ?? now,
      updatedAt: now,
    }
    this.state.providers = existing
      ? this.state.providers.map((p) => (p.id === provider.id ? provider : p))
      : [...this.state.providers, provider]
    this.save()
    return provider
  }

  private removeProvider(id: string): Response {
    this.find(this.state.providers, id, 'provider')
    this.state.providers = this.state.providers.filter((p) => p.id !== id)
    this.state.orbs = this.state.orbs.map((o) =>
      o.model?.providerId === id ? { ...o, model: undefined } : o,
    )
    this.save()
    return new Response(null, { status: 204 })
  }

  private models(id: string) {
    const provider = this.find(this.state.providers, id, 'provider')
    if (provider.kind === ECHO_KIND) return [{ id: 'echo-1', displayName: 'Echo 1' }]
    return provider.defaultModel ? [{ id: provider.defaultModel }] : []
  }

  // Context sources

  private saveSource(id: string | undefined, body: unknown): ContextSource {
    const request = body as SaveContextSourceRequest
    const name = required(request.name, 'name')
    if (!CONTEXT_KINDS.some((k) => k.kind === request.kind)) throw new HttpError(400, 'Unknown context kind.')
    const now = this.timestamp()
    const existing = id ? this.find(this.state.contextSources, id, 'context source') : undefined
    const base = {
      id: existing?.id ?? newId(),
      name,
      kind: request.kind,
      location: request.location?.trim() ?? '',
      syncEvery:
        request.kind === 'url' ? (request.syncEvery ?? existing?.syncEvery ?? 'off') : ('off' as const),
      input: request.input ?? '',
      createdAt: existing?.createdAt ?? now,
      updatedAt: now,
    }
    const source = { ...base, ...this.read(base, now) }
    this.state.contextSources = existing
      ? this.state.contextSources.map((c) => (c.id === source.id ? source : c))
      : [...this.state.contextSources, source]
    this.save()
    return source
  }

  private syncSource(id: string): ContextSource {
    const existing = this.find(this.state.contextSources, id, 'context source')
    const source = { ...existing, ...this.read(existing, this.timestamp()) }
    this.state.contextSources = this.state.contextSources.map((c) => (c.id === id ? source : c))
    this.save()
    return source
  }

  /** Notes are read as typed. Web pages cannot be fetched without a server, so they fail politely. */
  private read(source: Pick<ContextSource, 'kind' | 'input'>, now: string) {
    if (source.kind === 'text') {
      return {
        status: 'ready' as const,
        lastError: undefined,
        lastSyncedAt: now,
        contentLength: source.input.length,
      }
    }
    return {
      status: 'failed' as const,
      lastError: 'The live demo cannot read web pages. Self-host Orbs to use this source.',
      lastSyncedAt: undefined,
      contentLength: 0,
    }
  }

  private removeSource(id: string): Response {
    this.find(this.state.contextSources, id, 'context source')
    this.state.contextSources = this.state.contextSources.filter((c) => c.id !== id)
    this.state.orbs = this.state.orbs.map((o) => ({
      ...o,
      contextSourceIds: o.contextSourceIds.filter((sourceId) => sourceId !== id),
    }))
    this.save()
    return new Response(null, { status: 204 })
  }

  // Conversations

  private startConversation(body: unknown): Conversation {
    const { orbId } = (body ?? {}) as { orbId?: string }
    this.find(this.state.orbs, orbId ?? '', 'orb')
    const now = this.timestamp()
    const conversation: Conversation = {
      id: newId(),
      orbId: orbId!,
      title: 'New chat',
      createdAt: now,
      updatedAt: now,
      messages: [],
    }
    this.state.conversations = [...this.state.conversations, conversation]
    this.save()
    return conversation
  }

  /** Picks the model like the real ModelResolver: a pinned, enabled provider first, else "Auto". */
  private resolve(orb: Orb): { provider: Provider; model: string } {
    const enabled = this.state.providers.filter((p) => p.isEnabled)
    const pinned = orb.model && enabled.find((p) => p.id === orb.model!.providerId)
    if (pinned) return { provider: pinned, model: orb.model!.model }
    const fallback = enabled
      .filter((p) => p.defaultModel)
      .sort((a, b) => a.createdAt.localeCompare(b.createdAt))[0]
    if (!fallback) throw new HttpError(409, 'No model is available. Connect a provider under Models first.')
    return { provider: fallback, model: fallback.defaultModel! }
  }

  /** The sources that go into the prompt, with the same budget rules as the server's PromptBuilder. */
  private sourcesUsed(orb: Orb): ChatSource[] {
    const ready = this.state.contextSources.filter(
      (s) => orb.contextSourceIds.includes(s.id) && s.status === 'ready' && s.contentLength > 0,
    )
    if (ready.length === 0) return []
    const share = Math.floor(PROMPT_CONTEXT_CHARACTERS / ready.length)
    return ready.map((s) => ({
      id: s.id,
      name: s.name,
      kind: s.kind,
      characters: Math.min(s.contentLength, share),
      truncated: s.contentLength > share,
    }))
  }

  private send(id: string, body: unknown, signal?: AbortSignal): Response {
    const conversation = this.find(this.state.conversations, id, 'conversation')
    const content = required((body as { content?: string })?.content, 'content')
    const orb = this.find(this.state.orbs, conversation.orbId, 'orb')
    const { provider, model } = this.resolve(orb)
    const sources = this.sourcesUsed(orb)

    this.append(conversation, 'user', content)
    this.pending.delete(id)
    const match = provider.kind === ECHO_KIND ? /^\/tool (\S+)\s*(.*)$/.exec(content) : null
    const access = match ? (orb.toolPermissions?.[match[1]!] ?? 'ask') : 'block'
    const tool =
      match && access !== 'block' && DEMO_TOOLS.some((t) => t.name === match[1])
        ? { name: match[1]!, args: match[2] || '{}' }
        : null

    if (tool && access === 'ask') {
      const approvalId = newId()
      this.pending.set(id, { approvalId, tool, model, provider: provider.name })
      return sse(
        (async function* (): AsyncGenerator<ChatEvent> {
          yield { type: 'start', model, provider: provider.name, sources }
          yield {
            type: 'approval_required',
            approvalId,
            callId: 'demo-call',
            tool: tool.name,
            arguments: tool.args,
          }
        })(),
      )
    }

    const start: ChatEvent = { type: 'start', model, provider: provider.name, sources }
    const fallback = provider.kind === ECHO_KIND ? `You said: ${content}` : OFFLINE_REPLY
    return sse(this.reply(conversation, model, start, tool, tool ? 'allowed' : undefined, fallback, signal))
  }

  /** Answers a waiting tool call, like POST /api/conversations/{id}/approvals/{approvalId}. */
  private decide(id: string, approvalId: string | undefined, body: unknown, signal?: AbortSignal): Response {
    const conversation = this.find(this.state.conversations, id, 'conversation')
    const waiting = this.pending.get(id)
    if (!waiting || waiting.approvalId !== approvalId) {
      throw new HttpError(400, 'This request was already answered.')
    }
    this.pending.delete(id)
    const decision = (body as { decision?: string })?.decision ?? 'approve'
    if (decision === 'always') {
      this.state.orbs = this.state.orbs.map((o) =>
        o.id === conversation.orbId
          ? { ...o, toolPermissions: { ...o.toolPermissions, [waiting.tool.name]: 'allow' as const } }
          : o,
      )
      this.save()
    }
    const start: ChatEvent = {
      type: 'start',
      model: waiting.model,
      provider: waiting.provider,
      resumed: true,
    }
    const verdict = decision === 'deny' ? 'denied' : 'approved'
    return sse(this.reply(conversation, waiting.model, start, waiting.tool, verdict, '', signal))
  }

  /** Streams a reply: optionally one tool call first, then the words; then saves it. */
  private async *reply(
    conversation: Conversation,
    model: string,
    start: ChatEvent,
    tool: { name: string; args: string } | null,
    decision: string | undefined,
    fallback: string,
    signal?: AbortSignal,
  ): AsyncGenerator<ChatEvent> {
    yield start
    let text = fallback
    let toolCalls: Message['toolCalls']
    if (tool) {
      const ran =
        decision === 'denied'
          ? { result: 'The person declined this tool call.', isError: true }
          : runDemoTool(tool.name, tool.args, this.now())
      yield { type: 'tool_call', callId: 'demo-call', tool: tool.name, arguments: tool.args }
      await delay(this.wordDelay, signal)
      yield {
        type: 'tool_result',
        callId: 'demo-call',
        tool: tool.name,
        text: ran.result,
        isError: ran.isError,
        durationMs: 1,
        decision,
      }
      text = `${tool.name} said: ${ran.result}`
      toolCalls = [
        {
          tool: tool.name,
          arguments: tool.args,
          result: ran.result,
          isError: ran.isError,
          durationMs: 1,
          decision,
        },
      ]
    }
    for (const [i, word] of text.split(' ').entries()) {
      await delay(this.wordDelay, signal)
      yield { type: 'delta', text: i === 0 ? word : ` ${word}` }
    }
    const message = this.append(conversation, 'assistant', text, model, toolCalls)
    yield { type: 'done', messageId: message.id }
  }

  /** Same paging as the server: messages before `before`, oldest first. */
  private olderMessages(id: string, url: URL) {
    const before = url.searchParams.get('before') ?? '9999'
    const limit = Number(url.searchParams.get('limit') ?? 50)
    const older = this.find(this.state.conversations, id, 'conversation').messages.filter(
      (m) => m.createdAt < before,
    )
    return { messages: older.slice(-limit), hasMore: older.length > limit }
  }

  private append(
    conversation: Conversation,
    role: Message['role'],
    content: string,
    model?: string,
    toolCalls?: Message['toolCalls'],
  ): Message {
    const now = this.timestamp()
    const message: Message = { id: newId(), role, content, createdAt: now, model, toolCalls }
    const current = this.find(this.state.conversations, conversation.id, 'conversation')
    const title = current.title === 'New chat' && role === 'user' ? titleFrom(content) : current.title
    const updated = { ...current, title, updatedAt: now, messages: [...current.messages, message] }
    this.state.conversations = this.state.conversations.map((c) => (c.id === updated.id ? updated : c))
    this.save()
    return message
  }

  // Helpers

  private find<T extends { id: string }>(items: T[], id: string, what: string): T {
    const item = items.find((i) => i.id === id)
    if (!item) throw new HttpError(404, `That ${what} does not exist.`)
    return item
  }

  private timestamp(): string {
    return this.now().toISOString()
  }

  private load(): DemoState {
    try {
      const raw = globalThis.localStorage?.getItem(STORAGE_KEY)
      if (raw) return JSON.parse(raw) as DemoState
    } catch {
      // Unreadable or blocked storage: start fresh.
    }
    const fresh = seed(this.now())
    this.state = fresh
    this.save()
    return fresh
  }

  private save(): void {
    try {
      globalThis.localStorage?.setItem(STORAGE_KEY, JSON.stringify(this.state))
    } catch {
      // Storage full or blocked: the demo keeps working in memory.
    }
  }
}

function json(value: unknown, status = 200): Response {
  return new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
}

function required(value: string | undefined, field: string): string {
  const text = value?.trim()
  if (!text) throw new HttpError(400, `The ${field} is required.`)
  return text
}

function titleFrom(content: string): string {
  const firstLine = (content.split('\n', 1)[0] ?? '').trim()
  return firstLine.length <= 60 ? firstLine : `${firstLine.slice(0, 57)}...`
}

function newId(): string {
  return globalThis.crypto.randomUUID()
}

function delay(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) return reject(new DOMException('Aborted', 'AbortError'))
    if (ms <= 0) return resolve()
    const timer = setTimeout(resolve, ms)
    signal?.addEventListener(
      'abort',
      () => {
        clearTimeout(timer)
        reject(new DOMException('Aborted', 'AbortError'))
      },
      { once: true },
    )
  })
}

/** Wraps chat events as a server-sent events response, like the real API. */
function sse(events: AsyncGenerator<ChatEvent>): Response {
  const encoder = new TextEncoder()
  const stream = new ReadableStream<Uint8Array>({
    async pull(controller) {
      try {
        const next = await events.next()
        if (next.done) controller.close()
        else controller.enqueue(encoder.encode(`data: ${JSON.stringify(next.value)}\n\n`))
      } catch (error) {
        controller.error(error)
      }
    },
    cancel() {
      void events.return(undefined)
    },
  })
  return new Response(stream, { status: 200, headers: { 'Content-Type': 'text/event-stream' } })
}
