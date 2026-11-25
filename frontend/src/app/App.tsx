import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, RouterProvider, type RouteObject } from 'react-router'
import { AppShell } from '@/components/layout/AppShell'
import { ButtonLink, EmptyState } from '@/components/ui'
import { Orb } from '@/components/orb/Orb'
import { ChatPage } from '@/features/chat/ChatPage'
import { ContextPage } from '@/features/context/ContextPage'
import { ModelsPage } from '@/features/models/ModelsPage'
import { OrbEditorPage } from '@/features/orbs/OrbEditorPage'
import { OrbsPage } from '@/features/orbs/OrbsPage'
import { SettingsPage } from '@/features/settings/SettingsPage'
import { SettingsProvider } from './SettingsContext'

function NotFound() {
  return (
    <EmptyState>
      <Orb size={96} appearance={{ eyes: 'wide', mouth: 'open', accessory: 'none' }} />
      <h1>Nothing here</h1>
      <p>This page floated away.</p>
      <ButtonLink to="/" variant="primary">
        Back to your orbs
      </ButtonLink>
    </EmptyState>
  )
}

export const routes: RouteObject[] = [
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: <OrbsPage /> },
      { path: 'orbs/new', element: <OrbEditorPage /> },
      { path: 'orbs/:orbId', element: <OrbEditorPage /> },
      { path: 'orbs/:orbId/chat', element: <ChatPage /> },
      { path: 'orbs/:orbId/chat/:conversationId', element: <ChatPage /> },
      { path: 'context', element: <ContextPage /> },
      { path: 'models', element: <ModelsPage /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: '*', element: <NotFound /> },
    ],
  },
]

export function createQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { staleTime: 10_000, retry: 1 } } })
}

const router = createBrowserRouter(routes)
const queryClient = createQueryClient()

export function App() {
  return (
    <SettingsProvider>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </SettingsProvider>
  )
}
