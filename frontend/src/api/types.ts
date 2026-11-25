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
  messages: Message[]
}

export type ChatEvent =
  | { type: 'start'; model: string }
  | { type: 'delta'; text: string }
  | { type: 'done'; messageId: string }
  | { type: 'error'; text: string }

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
}
