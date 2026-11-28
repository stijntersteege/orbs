import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  accountApi,
  authApi,
  contextApi,
  conversationsApi,
  jobsApi,
  toolsApi,
  memoriesApi,
  mcpApi,
  integrationsApi,
  schedulesApi,
  skillsApi,
  notificationsApi,
  channelsApi,
  voiceApi,
  orbsApi,
  providersApi,
  usersApi,
} from './endpoints'
import type { SaveContextSourceRequest, SaveOrbRequest, SaveProviderRequest } from './types'

/** One place for cache keys, so invalidation stays consistent. */
export const keys = {
  orbs: ['orbs'] as const,
  orb: (id: string) => ['orbs', id] as const,
  providers: ['providers'] as const,
  providerKinds: ['providers', 'kinds'] as const,
  models: (providerId: string) => ['providers', providerId, 'models'] as const,
  context: ['context'] as const,
  contextKinds: ['context', 'kinds'] as const,
  conversations: (orbId?: string) => ['conversations', orbId ?? 'all'] as const,
  conversation: (id: string) => ['conversation', id] as const,
  authStatus: ['auth', 'status'] as const,
  account: ['account'] as const,
  users: ['users'] as const,
  jobs: ['jobs'] as const,
  tools: ['tools'] as const,
  memories: (orbId: string) => ['memories', orbId] as const,
  mcpServers: ['mcp-servers'] as const,
  integrations: ['integrations'] as const,
  schedules: ['schedules'] as const,
  skills: ['skills'] as const,
  notifications: ['account', 'notifications'] as const,
  channels: ['channels'] as const,
  voice: ['voice'] as const,
  channelKinds: ['channels', 'kinds'] as const,
  integrationKinds: ['integrations', 'kinds'] as const,
}

export const useOrbs = () => useQuery({ queryKey: keys.orbs, queryFn: orbsApi.list })

export const useOrb = (id: string | undefined) =>
  useQuery({ queryKey: keys.orb(id ?? ''), queryFn: () => orbsApi.get(id!), enabled: !!id })

export function useSaveOrb() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id?: string; body: SaveOrbRequest }) =>
      id ? orbsApi.update(id, body) : orbsApi.create(body),
    onSuccess: (orb) => {
      client.setQueryData(keys.orb(orb.id), orb)
      void client.invalidateQueries({ queryKey: keys.orbs })
    },
  })
}

export function useDeleteOrb() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: orbsApi.remove,
    onSuccess: () => client.invalidateQueries({ queryKey: keys.orbs }),
  })
}

export const useProviderKinds = () =>
  useQuery({ queryKey: keys.providerKinds, queryFn: providersApi.kinds, staleTime: Infinity })

export const useProviders = () => useQuery({ queryKey: keys.providers, queryFn: providersApi.list })

export const useModels = (providerId: string | undefined) =>
  useQuery({
    queryKey: keys.models(providerId ?? ''),
    queryFn: () => providersApi.models(providerId!),
    enabled: !!providerId,
    retry: false,
  })

export function useSaveProvider() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id?: string; body: SaveProviderRequest }) =>
      id ? providersApi.update(id, body) : providersApi.create(body),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.providers }),
  })
}

export function useDeleteProvider() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: providersApi.remove,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.providers })
      void client.invalidateQueries({ queryKey: keys.orbs })
    },
  })
}

export const useContextKinds = () =>
  useQuery({ queryKey: keys.contextKinds, queryFn: contextApi.kinds, staleTime: Infinity })

export const useContextSources = () => useQuery({ queryKey: keys.context, queryFn: contextApi.list })

export function useSaveContextSource() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id?: string; body: SaveContextSourceRequest }) =>
      id ? contextApi.update(id, body) : contextApi.create(body),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.context }),
  })
}

export function useSyncContextSource() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: contextApi.sync,
    onSuccess: () => client.invalidateQueries({ queryKey: keys.context }),
  })
}

export function useUploadContextFile() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ file, name }: { file: File; name?: string }) => contextApi.upload(file, name),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.context }),
  })
}

export function useDeleteContextSource() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: contextApi.remove,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.context })
      void client.invalidateQueries({ queryKey: keys.orbs })
    },
  })
}

export const useConversations = (orbId?: string) =>
  useQuery({ queryKey: keys.conversations(orbId), queryFn: () => conversationsApi.list(orbId) })

export const useConversation = (id: string | undefined) =>
  useQuery({ queryKey: keys.conversation(id ?? ''), queryFn: () => conversationsApi.get(id!), enabled: !!id })

export const useAuthStatus = () => useQuery({ queryKey: keys.authStatus, queryFn: authApi.status })

/** The signed-in account. Fails with a 401 ApiError when nobody is signed in. */
export const useAccount = () => useQuery({ queryKey: keys.account, queryFn: accountApi.get, retry: false })

export const useUsers = () => useQuery({ queryKey: keys.users, queryFn: usersApi.list })

/** Refreshes everything after signing in or out: what you can see depends on who you are. */
export function useResetSession() {
  const client = useQueryClient()
  return () => client.resetQueries()
}

export const useJobs = () => useQuery({ queryKey: keys.jobs, queryFn: jobsApi.list, refetchInterval: 15_000 })

export const useTools = () => useQuery({ queryKey: keys.tools, queryFn: toolsApi.list })

export const useMcpServers = () => useQuery({ queryKey: keys.mcpServers, queryFn: mcpApi.list })

export const useIntegrationKinds = () =>
  useQuery({ queryKey: keys.integrationKinds, queryFn: integrationsApi.kinds, staleTime: Infinity })

export const useIntegrations = () => useQuery({ queryKey: keys.integrations, queryFn: integrationsApi.list })

export const useMemories = (orbId: string | undefined) =>
  useQuery({
    queryKey: keys.memories(orbId ?? ''),
    queryFn: () => memoriesApi.list(orbId!),
    enabled: !!orbId,
  })

export const useSkills = () => useQuery({ queryKey: keys.skills, queryFn: skillsApi.list })

export const useSchedules = () => useQuery({ queryKey: keys.schedules, queryFn: () => schedulesApi.list() })

export const useNotificationSettings = () =>
  useQuery({ queryKey: keys.notifications, queryFn: notificationsApi.settings })

export const useChannels = () => useQuery({ queryKey: keys.channels, queryFn: channelsApi.list })

export const useChannelKinds = () =>
  useQuery({ queryKey: keys.channelKinds, queryFn: channelsApi.kinds, staleTime: Infinity })

export const useVoiceStatus = () =>
  useQuery({ queryKey: keys.voice, queryFn: voiceApi.status, staleTime: 60_000 })
