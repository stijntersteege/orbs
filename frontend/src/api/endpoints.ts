import { http, request } from './client'
import { readServerSentEvents } from './sse'
import type {
  ContextContent,
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
  Memory,
  OrbImportResult,
  OrbTemplate,
  SaveSkillRequest,
  Skill,
  McpServer,
  McpServerTools,
  SaveMcpServerRequest,
  Integration,
  Schedule,
  NotificationSettings,
  Channel,
  VoiceStatus,
  ChannelKind,
  SaveChannelRequest,
  SaveScheduleRequest,
  IntegrationKind,
  SaveIntegrationRequest,
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
  /** The orb as a template to share. */
  template: (id: string) => http.get<OrbTemplate>(`/api/orbs/${id}/template`),
  /** Creates a new orb from a template file's JSON. */
  importTemplate: (template: unknown) => http.post<OrbImportResult>('/api/orbs/import', template),
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
  /** Uploads a PDF, Word, Markdown or text document, or an image. */
  upload: async (file: File, name?: string) => {
    const form = new FormData()
    form.append('file', file)
    if (name) form.append('name', name)
    return (
      await request('/api/context-sources/files', { method: 'POST', body: form })
    ).json() as Promise<ContextSource>
  },
  fileUrl: (id: string) => `/api/context-sources/${id}/file`,
  /** What the source holds, as orbs read it, and how it can be edited. */
  content: (id: string) => http.get<ContextContent>(`/api/context-sources/${id}/content`),
  /** Saves edited text back into a text file. */
  replaceText: (id: string, text: string) =>
    http.put<ContextContent>(`/api/context-sources/${id}/file`, { text }),
  /** The original file, with the signed-in session. */
  download: async (id: string) => (await request(`/api/context-sources/${id}/file`)).blob(),
  remove: (id: string) => http.delete(`/api/context-sources/${id}`),
}

export const conversationsApi = {
  list: (orbId?: string) =>
    http.get<ConversationSummary[]>(
      `/api/conversations${orbId ? `?orbId=${encodeURIComponent(orbId)}` : ''}`,
    ),
  get: (id: string) => http.get<Conversation>(`/api/conversations/${id}`),
  start: (orbId: string, withOrbs?: string[]) =>
    http.post<Conversation>('/api/conversations', { orbId, with: withOrbs }),
  remove: (id: string) => http.delete(`/api/conversations/${id}`),
  /** Gives the chat a file; `remember` gives it to the chat's orb for every chat instead. */
  attach: (id: string, contextSourceId: string, remember = false) =>
    http.post<ConversationSummary>(`/api/conversations/${id}/attachments`, { contextSourceId, remember }),
  detach: (id: string, contextSourceId: string) =>
    http.delete(`/api/conversations/${id}/attachments/${contextSourceId}`),
  rename: (id: string, title: string) => http.put<ConversationSummary>(`/api/conversations/${id}`, { title }),
  /** Deletes one message; the orb no longer sees it. */
  deleteMessage: (id: string, messageId: string) =>
    http.delete(`/api/conversations/${id}/messages/${messageId}`),
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
  /** A new password signs that person out everywhere. */
  update: (id: string, body: { displayName: string; isAdmin: boolean; newPassword?: string }) =>
    http.put<User>(`/api/users/${id}`, body),
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

export const integrationsApi = {
  kinds: () => http.get<IntegrationKind[]>('/api/integrations/kinds'),
  list: () => http.get<Integration[]>('/api/integrations'),
  create: (body: SaveIntegrationRequest) => http.post<Integration>('/api/integrations', body),
  update: (id: string, body: SaveIntegrationRequest) =>
    http.put<Integration>(`/api/integrations/${id}`, body),
  remove: (id: string) => http.delete(`/api/integrations/${id}`),
  test: (id: string) => http.post<{ ok: boolean; error?: string }>(`/api/integrations/${id}/test`),
}

export const memoriesApi = {
  list: (orbId: string) => http.get<Memory[]>(`/api/orbs/${orbId}/memories`),
  add: (orbId: string, content: string, shared = false) =>
    http.post<Memory>(`/api/orbs/${orbId}/memories`, { content, shared }),
  /** `shared`: true for all orbs; false keeps it for `orbId` only; undefined leaves it as it is. */
  edit: (id: string, content: string, shared?: boolean, orbId?: string) =>
    http.put<Memory>(`/api/memories/${id}`, { content, shared, orbId }),
  forget: (id: string) => http.delete(`/api/memories/${id}`),
}

export const skillsApi = {
  list: () => http.get<Skill[]>('/api/skills'),
  create: (body: SaveSkillRequest) => http.post<Skill>('/api/skills', body),
  update: (id: string, body: SaveSkillRequest) => http.put<Skill>(`/api/skills/${id}`, body),
  remove: (id: string) => http.delete(`/api/skills/${id}`),
  /** Adds a skill from a SKILL.md file's text. One with the same name is replaced. */
  import: (markdown: string) => http.post<Skill>('/api/skills/import', { markdown }),
  /** The skill as a SKILL.md file. */
  export: async (id: string) => (await request(`/api/skills/${id}/export`)).text(),
  /** Saves the tools a reply used as a skill. */
  fromReply: (conversationId: string, messageId: string, name: string) =>
    http.post<Skill>(`/api/conversations/${conversationId}/messages/${messageId}/skill`, { name }),
}

export const schedulesApi = {
  list: (orbId?: string) => http.get<Schedule[]>(`/api/schedules${orbId ? `?orbId=${orbId}` : ''}`),
  create: (body: SaveScheduleRequest) => http.post<Schedule>('/api/schedules', body),
  update: (id: string, body: SaveScheduleRequest) => http.put<Schedule>(`/api/schedules/${id}`, body),
  remove: (id: string) => http.delete(`/api/schedules/${id}`),
  run: async (id: string) => {
    await request(`/api/schedules/${id}/run`, { method: 'POST' })
  },
}

export const notificationsApi = {
  key: () => http.get<{ publicKey: string }>('/api/push/key'),
  subscribe: async (body: { endpoint: string; keys: { p256dh: string; auth: string } }) => {
    await request('/api/push/subscriptions', { method: 'POST', body: JSON.stringify(body) })
  },
  test: () => http.post<{ delivered: number }>('/api/push/test'),
  settings: () => http.get<NotificationSettings>('/api/account/notifications'),
  saveSettings: (body: { quietFrom?: string; quietUntil?: string; timeZone?: string }) =>
    http.put<NotificationSettings>('/api/account/notifications', body),
}

export const channelsApi = {
  kinds: () => http.get<ChannelKind[]>('/api/channels/kinds'),
  list: () => http.get<Channel[]>('/api/channels'),
  create: (body: SaveChannelRequest) => http.post<Channel>('/api/channels', body),
  /** A blank secret keeps the saved one. */
  update: (id: string, body: SaveChannelRequest) => http.put<Channel>(`/api/channels/${id}`, body),
  pairingCode: (id: string) => http.post<Channel>(`/api/channels/${id}/pairing-code`),
  unpair: (id: string, chatId: string) => http.delete(`/api/channels/${id}/chats/${chatId}`),
  remove: (id: string) => http.delete(`/api/channels/${id}`),
  avatar: async (id: string, png: Blob) =>
    (
      await request(`/api/channels/${id}/avatar`, {
        method: 'PUT',
        body: png,
        headers: { 'Content-Type': 'image/png' },
      })
    ).json() as Promise<{
      note?: string
    }>,
}

export const voiceApi = {
  status: () => http.get<VoiceStatus>('/api/voice'),
  transcribe: async (audio: Blob) => {
    const form = new FormData()
    form.append('audio', audio, `speech.${audio.type.includes('mp4') ? 'm4a' : 'webm'}`)
    return (await request('/api/voice/transcribe', { method: 'POST', body: form })).json() as Promise<{
      text: string
    }>
  },
  speak: async (text: string, voice?: string, orbId?: string) =>
    (
      await request('/api/voice/speak', { method: 'POST', body: JSON.stringify({ text, voice, orbId }) })
    ).blob(),
}
