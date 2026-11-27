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
}

export interface SaveOrbRequest {
  name: string
  description?: string
  instructions?: string
  appearance?: OrbAppearance
  model?: ModelRef | null
  contextSourceIds?: string[]
}

export interface ProviderKind {
  kind: string
  displayName: string
  description: string
  requiresBaseUrl: boolean
  requiresApiKey: boolean
  defaultBaseUrl?: string
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
}

export interface SaveProviderRequest {
  name: string
  kind: string
  baseUrl?: string
  /** On update: undefined keeps the stored key, '' removes it. */
  apiKey?: string
  defaultModel?: string
  isEnabled?: boolean
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
}

export interface SaveContextSourceRequest {
  name: string
  kind: string
  location?: string
  input?: string
}

export type MessageRole = 'user' | 'assistant'

export interface Message {
  id: string
  role: MessageRole
  content: string
  createdAt: string
}

export interface ConversationSummary {
  id: string
  orbId: string
  title: string
  createdAt: string
  updatedAt: string
}

export interface Conversation extends ConversationSummary {
  /** The newest messages, oldest first. */
  messages: Message[]
  /** True when older messages can be loaded with `conversationsApi.messages`. */
  hasMoreMessages?: boolean
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
}

export type ChatEvent =
  | { type: 'start'; model: string; provider?: string; sources?: ChatSource[] }
  | { type: 'delta'; text: string }
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
