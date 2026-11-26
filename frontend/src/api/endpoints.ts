import { http, request } from './client'
import { readServerSentEvents } from './sse'
import type {
  ChatEvent,
  ContextKind,
  ContextSource,
  Conversation,
  ConversationSummary,
  ModelInfo,
  Orb,
  Provider,
  ProviderKind,
  SaveContextSourceRequest,
  SaveOrbRequest,
  SaveProviderRequest,
} from './types'

export const orbsApi = {
  list: () => http.get<Orb[]>('/api/orbs'),
  get: (id: string) => http.get<Orb>(`/api/orbs/${id}`),
  create: (body: SaveOrbRequest) => http.post<Orb>('/api/orbs', body),
  update: (id: string, body: SaveOrbRequest) => http.put<Orb>(`/api/orbs/${id}`, body),
  remove: (id: string) => http.delete(`/api/orbs/${id}`),
}

export const providersApi = {
  kinds: () => http.get<ProviderKind[]>('/api/providers/kinds'),
  list: () => http.get<Provider[]>('/api/providers'),
  create: (body: SaveProviderRequest) => http.post<Provider>('/api/providers', body),
  update: (id: string, body: SaveProviderRequest) => http.put<Provider>(`/api/providers/${id}`, body),
  remove: (id: string) => http.delete(`/api/providers/${id}`),
  models: (id: string) => http.get<ModelInfo[]>(`/api/providers/${id}/models`),
}

export const contextApi = {
  kinds: () => http.get<ContextKind[]>('/api/context-sources/kinds'),
  list: () => http.get<ContextSource[]>('/api/context-sources'),
  create: (body: SaveContextSourceRequest) => http.post<ContextSource>('/api/context-sources', body),
  update: (id: string, body: SaveContextSourceRequest) =>
    http.put<ContextSource>(`/api/context-sources/${id}`, body),
  sync: (id: string) => http.post<ContextSource>(`/api/context-sources/${id}/sync`),
  remove: (id: string) => http.delete(`/api/context-sources/${id}`),
}

export const conversationsApi = {
  list: (orbId?: string) =>
    http.get<ConversationSummary[]>(
      `/api/conversations${orbId ? `?orbId=${encodeURIComponent(orbId)}` : ''}`,
    ),
  get: (id: string) => http.get<Conversation>(`/api/conversations/${id}`),
  start: (orbId: string) => http.post<Conversation>('/api/conversations', { orbId }),
  remove: (id: string) => http.delete(`/api/conversations/${id}`),

  /** Sends a message and yields the reply as it streams in. */
  async *send(id: string, content: string, signal?: AbortSignal): AsyncGenerator<ChatEvent> {
    const response = await request(`/api/conversations/${id}/messages`, {
      method: 'POST',
      body: JSON.stringify({ content }),
      headers: { Accept: 'text/event-stream' },
      signal,
    })
    if (!response.body) throw new Error('The server sent no reply stream.')
    yield* readServerSentEvents<ChatEvent>(response.body)
  },
}
