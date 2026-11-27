// Contracts of the Orbs API. They mirror the backend DTOs (see /openapi/v1.json).

export const ORB_COLORS = ['plasma', 'ocean', 'sky', 'mint', 'sun', 'coral', 'bubblegum'] as const
export const ORB_SHAPES = ['circle', 'blob', 'squircle', 'drop'] as const
export const ORB_EYES = ['dots', 'happy', 'wide', 'sleepy', 'wink', 'visor'] as const
export const ORB_MOUTHS = ['smile', 'open', 'flat', 'grin', 'none'] as const
export const ORB_ACCESSORIES = ['none', 'antenna', 'halo', 'sprout', 'ring', 'headphones', 'crown'] as const

export type OrbColor = (typeof ORB_COLORS)[number]
export type OrbShape = (typeof ORB_SHAPES)[number]
export type OrbEyes = (typeof ORB_EYES)[number]
export type OrbMouth = (typeof ORB_MOUTHS)[number]
export type OrbAccessory = (typeof ORB_ACCESSORIES)[number]

export interface OrbAppearance {
  color: OrbColor
  shape: OrbShape
  eyes: OrbEyes
  mouth: OrbMouth
  accessory: OrbAccessory
  blush: boolean
  glow: boolean
  floating: boolean
}

export const DEFAULT_APPEARANCE: OrbAppearance = {
  color: 'plasma',
  shape: 'circle',
  eyes: 'happy',
  mouth: 'smile',
  accessory: 'antenna',
  blush: true,
  glow: true,
  floating: true,
}

export interface ModelRef {
  providerId: string
  model: string
}

export type ToolAccess = 'allow' | 'ask' | 'block'

export interface Orb {
  id: string
  name: string
  description: string
  instructions: string
  appearance: OrbAppearance
  model?: ModelRef
  contextSourceIds: string[]
  createdAt: string
  updatedAt: string
  /** Tools not listed here ask first. */
  toolPermissions?: Record<string, ToolAccess>
  /** How the orb sounds when it reads replies aloud. */
  voice?: string
}

export interface SaveOrbRequest {
  name: string
  description?: string
  instructions?: string
  appearance?: OrbAppearance
  model?: ModelRef | null
  contextSourceIds?: string[]
  /** Omit to keep the current permissions. */
  toolPermissions?: Record<string, ToolAccess>
  voice?: string
}

export interface ProviderKind {
  kind: string
  displayName: string
  description: string
  requiresBaseUrl: boolean
  requiresApiKey: boolean
  defaultBaseUrl?: string
  /** True when this kind can embed text for retrieval. */
  supportsEmbeddings?: boolean
  /** True when this kind can listen (speech-to-text) or speak (text-to-speech). */
  supportsSpeech?: boolean
}

export interface Provider {
  id: string
  name: string
  kind: string
  baseUrl?: string
  hasApiKey: boolean
  defaultModel?: string
  isEnabled: boolean
  createdAt: string
  updatedAt: string
  /** The model that embeds context for retrieval. */
  embeddingModel?: string
  speechToTextModel?: string
  textToSpeechModel?: string
}

export interface SaveProviderRequest {
  name: string
  kind: string
  baseUrl?: string
  /** On update: undefined keeps the stored key, '' removes it. */
  apiKey?: string
  defaultModel?: string
  isEnabled?: boolean
  embeddingModel?: string
  speechToTextModel?: string
  textToSpeechModel?: string
}

export interface ModelInfo {
  id: string
  displayName?: string
}

export interface ContextKind {
  kind: string
  displayName: string
  description: string
  usesLocation: boolean
  usesInput: boolean
}

export type SyncInterval = 'off' | 'hourly' | 'daily' | 'weekly'

export type ContextSourceStatus = 'pending' | 'ready' | 'failed'

export interface ContextSource {
  id: string
  name: string
  kind: string
  location: string
  input: string
  status: ContextSourceStatus
  lastError?: string
  lastSyncedAt?: string
  contentLength: number
  createdAt: string
  updatedAt: string
  /** Passages embedded for retrieval; 0 until indexed. */
  indexedChunks?: number
  syncEvery?: SyncInterval
  nextSyncAt?: string
  indexedAt?: string
}

export interface SaveContextSourceRequest {
  name: string
  kind: string
  location?: string
  input?: string
  /** Only for kinds that read from a location, such as web pages. */
  syncEvery?: SyncInterval
}

export type MessageRole = 'user' | 'assistant'

export interface Message {
  id: string
  role: MessageRole
  content: string
  createdAt: string
  /** The model that wrote an assistant message. */
  model?: string
  /** Tools the orb used while writing this reply: the audit trail. */
  toolCalls?: ToolCallRecord[]
  /** The orb that wrote an assistant message. Missing in older chats: then it is the chat's orb. */
  orbId?: string
}

export interface ToolCallRecord {
  tool: string
  /** The arguments as JSON. */
  arguments: string
  result: string
  isError: boolean
  durationMs: number
  /** "allowed" by the orb's settings, "approved" or "denied" by you, or "blocked". */
  decision?: string
}

/** A tool call waiting for your yes or no. */
export interface Approval {
  id: string
  callId: string
  tool: string
  arguments: string
}

export type ApprovalDecision = 'approve' | 'deny' | 'always'

export interface ToolInfo {
  name: string
  displayName: string
  description: string
  risk: 'read' | 'write'
  /** Runs without asking unless the orb's setting says otherwise. */
  allowedByDefault?: boolean
}

export interface ConversationSummary {
  id: string
  orbId: string
  title: string
  createdAt: string
  updatedAt: string
  /** Every orb in the chat; more than one makes it a group chat. */
  orbIds?: string[]
}

export interface Conversation extends ConversationSummary {
  /** The newest messages, oldest first. */
  messages: Message[]
  /** True when older messages can be loaded with `conversationsApi.messages`. */
  hasMoreMessages?: boolean
  pendingApproval?: Approval
}

export interface MessagePage {
  messages: Message[]
  hasMore: boolean
}

/** A source the orb read to answer, as reported when a reply starts. */
export interface ChatSource {
  id: string
  name: string
  kind: string
  characters: number
  truncated: boolean
  /** Set when retrieval picked passages from this source. */
  passages?: number
}

export type ChatEvent =
  | {
      type: 'start'
      model: string
      provider?: string
      sources?: ChatSource[]
      resumed?: boolean
      /** Which orb answers; in a group chat several replies follow each other. */
      orbId?: string
      orbName?: string
    }
  | { type: 'approval_required'; approvalId: string; callId: string; tool: string; arguments: string }
  | { type: 'delta'; text: string }
  | { type: 'tool_call'; callId: string; tool: string; arguments: string }
  | {
      type: 'tool_result'
      callId: string
      tool: string
      text: string
      isError: boolean
      durationMs: number
      decision?: string
    }
  | { type: 'done'; messageId: string }
  | { type: 'error'; text: string }

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
}

// Accounts

export interface AuthStatus {
  setupRequired: boolean
  passwordSignIn: boolean
  passkeys: boolean
  oidc?: { displayName: string }
}

export interface User {
  id: string
  email: string
  displayName: string
  isAdmin: boolean
  hasPassword: boolean
  createdAt: string
}

export interface SignInResult {
  token: string
  expiresAt: string
  user: User
}

export interface Passkey {
  id: string
  name: string
  createdAt: string
  lastUsedAt?: string
}

export interface Session {
  id: string
  description: string
  createdAt: string
  lastSeenAt: string
  current: boolean
}

export interface Account {
  user: User
  passkeys: Passkey[]
  sessions: Session[]
  hasExternalLogin: boolean
}

export interface PasskeyOptions {
  ceremonyId: string
  options: string
}

export interface CreateUserRequest {
  email: string
  displayName?: string
  password: string
  isAdmin?: boolean
}

// Background jobs

export interface Job {
  id: string
  kind: string
  status: 'pending' | 'running' | 'succeeded' | 'failed'
  runAt: string
  attempts: number
  maxAttempts: number
  result?: string
  lastError?: string
  createdAt: string
  finishedAt?: string
}

// MCP servers

export interface McpServer {
  id: string
  name: string
  transport: 'http' | 'stdio'
  location: string
  hasAuthorization: boolean
  isEnabled: boolean
  lastError?: string
  createdAt: string
}

export interface SaveMcpServerRequest {
  name: string
  transport: 'http' | 'stdio'
  location: string
  /** On update: undefined keeps the stored value, '' removes it. */
  authorization?: string
  isEnabled?: boolean
}

export interface McpServerTools {
  tools: ToolInfo[]
  error?: string
}

// Integrations: apps that bring tools

export interface IntegrationField {
  key: string
  label: string
  required: boolean
  secret: boolean
  placeholder?: string
  hint?: string
  options?: string[]
}

export interface IntegrationKind {
  kind: string
  displayName: string
  description: string
  fields: IntegrationField[]
}

export interface Integration {
  id: string
  kind: string
  name: string
  settings: Record<string, string>
  hasSecret: boolean
  isEnabled: boolean
  tools: string[]
  createdAt: string
}

export interface SaveIntegrationRequest {
  kind: string
  name: string
  settings?: Record<string, string>
  /** On update: undefined keeps the stored secret, '' removes it. */
  secret?: string
  isEnabled?: boolean
}

// Memory

export interface Memory {
  id: string
  /** Empty for a memory every orb shares. */
  orbId?: string
  shared: boolean
  content: string
  source: 'manual' | 'learned'
  conversationId?: string
  createdAt: string
  updatedAt: string
}

// Orb templates

/** An orb as one shareable file. Leaves out the model, context, chats and memories, which belong to one account. */
export interface OrbTemplate {
  format: 'orbs-template'
  version: number
  orb: Pick<
    SaveOrbRequest,
    'name' | 'description' | 'instructions' | 'appearance' | 'toolPermissions' | 'voice'
  >
  schedules?: {
    name: string
    prompt: string
    trigger: TriggerKind
    cron?: string
    timeZone?: string
    feedUrl?: string
    isEnabled?: boolean
  }[]
}

export interface OrbImportResult {
  orb: Orb
  /** How many of the template's schedules came with it. */
  schedules: number
}

// Skills

/** A routine every orb can follow, in the SKILL.md format. */
export interface Skill {
  id: string
  /** Lower case, words joined by '-', such as "weekly-review". */
  name: string
  /** What the skill does and when to use it. */
  description: string
  /** The steps, in Markdown. */
  instructions: string
  createdAt: string
  updatedAt: string
}

export interface SaveSkillRequest {
  name: string
  description: string
  instructions: string
}

// Schedules

export type TriggerKind = 'time' | 'webhook' | 'rss' | 'email'

export interface Schedule {
  id: string
  orbId: string
  name: string
  prompt: string
  trigger: TriggerKind
  cron?: string
  timeZone: string
  feedUrl?: string
  integrationId?: string
  isEnabled: boolean
  nextCheckAt?: string
  lastRunAt?: string
  conversationId?: string
  createdAt: string
  /** Only right after creating a webhook schedule: the secret address. */
  webhookPath?: string
}

export interface SaveScheduleRequest {
  orbId: string
  name: string
  prompt: string
  trigger: TriggerKind
  cron?: string
  timeZone?: string
  feedUrl?: string
  integrationId?: string
  isEnabled?: boolean
}

// Notifications

export interface NotificationSettings {
  quietFrom?: string
  quietUntil?: string
  timeZone: string
  devices: number
}

// Channels

export interface ChannelKind {
  kind: string
  displayName: string
  description: string
  fields: IntegrationField[]
}

export interface Channel {
  id: string
  orbId: string
  kind: string
  address?: string
  settings: Record<string, string>
  isEnabled: boolean
  lastError?: string
  chats: { id: string; name: string; pairedAt: string; conversationId?: string }[]
  createdAt: string
  /** Only right after connecting or asking for a new one. */
  pairingCode?: string
  note?: string
}

export interface SaveChannelRequest {
  orbId: string
  kind: string
  settings?: Record<string, string>
  secret?: string
  isEnabled?: boolean
}

// Voice

export interface VoiceStatus {
  canListen: boolean
  canSpeak: boolean
  voices: string[]
}
