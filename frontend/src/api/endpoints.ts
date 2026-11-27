import { http, request } from './client'
import { readServerSentEvents } from './sse'
import type {
  ApprovalDecision,
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
  Account,
  AuthStatus,
  CreateUserRequest,
  Job,
  MessagePage,
  ToolInfo,
  McpServer,
  McpServerTools,
  SaveMcpServerRequest,
  Passkey,
  PasskeyOptions,
  SignInResult,
  User,
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
  /** Older messages, written before `before` (an ISO timestamp), oldest first. */
  messages: (id: string, before: string, limit = 50) =>
    http.get<MessagePage>(
      `/api/conversations/${id}/messages?before=${encodeURIComponent(before)}&limit=${limit}`,
    ),

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

  /** Answers a tool call that waits for approval and yields the rest of the reply. */
  async *decide(
    id: string,
    approvalId: string,
    decision: ApprovalDecision,
    signal?: AbortSignal,
  ): AsyncGenerator<ChatEvent> {
    const response = await request(`/api/conversations/${id}/approvals/${approvalId}`, {
      method: 'POST',
      body: JSON.stringify({ decision }),
      headers: { Accept: 'text/event-stream' },
      signal,
    })
    if (!response.body) throw new Error('The server sent no reply stream.')
    yield* readServerSentEvents<ChatEvent>(response.body)
  },
}

export const authApi = {
  status: () => http.get<AuthStatus>('/api/auth/status'),
  setup: (body: { email: string; displayName?: string; password: string }) =>
    http.post<SignInResult>('/api/auth/setup', body),
  signIn: (body: { email: string; password: string }) => http.post<SignInResult>('/api/auth/sign-in', body),
  passkeyOptions: () => http.post<PasskeyOptions>('/api/auth/passkeys/sign-in/options'),
  passkeySignIn: (body: { ceremonyId: string; response: string }) =>
    http.post<SignInResult>('/api/auth/passkeys/sign-in', body),
  signOut: async () => {
    await request('/api/auth/sign-out', { method: 'POST' })
  },
  /** Where the browser goes to sign in with the configured OpenID Connect provider. */
  oidcUrl: (returnTo = '/') => `/api/auth/oidc/start?returnTo=${encodeURIComponent(returnTo)}`,
}

export const accountApi = {
  get: () => http.get<Account>('/api/account'),
  update: (displayName: string) => http.put<User>('/api/account', { displayName }),
  changePassword: async (body: { currentPassword?: string; newPassword: string }) => {
    await request('/api/account/password', { method: 'POST', body: JSON.stringify(body) })
  },
  revokeSession: (id: string) => http.delete(`/api/account/sessions/${id}`),
  passkeyOptions: () => http.post<PasskeyOptions>('/api/account/passkeys/options'),
  addPasskey: (body: { ceremonyId: string; response: string; name?: string }) =>
    http.post<Passkey>('/api/account/passkeys', body),
  removePasskey: (id: string) => http.delete(`/api/account/passkeys/${id}`),
}

export const usersApi = {
  list: () => http.get<User[]>('/api/users'),
  create: (body: CreateUserRequest) => http.post<User>('/api/users', body),
  remove: (id: string) => http.delete(`/api/users/${id}`),
}

export const jobsApi = {
  list: () => http.get<Job[]>('/api/jobs'),
}

export const toolsApi = {
  list: () => http.get<ToolInfo[]>('/api/tools'),
}

export const mcpApi = {
  list: () => http.get<McpServer[]>('/api/mcp-servers'),
  create: (body: SaveMcpServerRequest) => http.post<McpServer>('/api/mcp-servers', body),
  update: (id: string, body: SaveMcpServerRequest) => http.put<McpServer>(`/api/mcp-servers/${id}`, body),
  remove: (id: string) => http.delete(`/api/mcp-servers/${id}`),
  tools: (id: string) => http.get<McpServerTools>(`/api/mcp-servers/${id}/tools`),
}
