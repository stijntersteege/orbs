import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, createHashRouter, RouterProvider, type RouteObject } from 'react-router'
import { AppShell, PageLayout } from '@/components/layout/AppShell'
import { ButtonLink, EmptyState } from '@/components/ui'
import { Orb } from '@/components/orb/Orb'
import { ChatPage } from '@/features/chat/ChatPage'
import { ContextPage } from '@/features/context/ContextPage'
import { ModelsPage } from '@/features/models/ModelsPage'
import { OrbEditorPage } from '@/features/orbs/OrbEditorPage'
import { OrbsPage } from '@/features/orbs/OrbsPage'
import { SettingsPage } from '@/features/settings/SettingsPage'
import { IS_DEMO } from '@/demo/mode'
import { SettingsProvider } from './SettingsContext'

function NotFound() {
  return (
    <EmptyState>
      <Orb size={96} appearance={{ eyes: 'wide', mouth: 'open', accessory: 'none' }} />
      <h1>Nothing here</h1>
      <p>This page floated away.</p>
      <ButtonLink to="/" variant="primary">
        Back to chat
      </ButtonLink>
    </EmptyState>
  )
}

export const routes: RouteObject[] = [
  {
    path: '/',
    element: <AppShell />,
    children: [
      // Chatting is the home screen. Everything else sits behind the menu.
      { index: true, element: <ChatPage /> },
      { path: 'orbs/:orbId/chat', element: <ChatPage /> },
      { path: 'orbs/:orbId/chat/:conversationId', element: <ChatPage /> },
      {
        element: <PageLayout />,
        children: [
          { path: 'orbs', element: <OrbsPage /> },
          { path: 'orbs/new', element: <OrbEditorPage /> },
          { path: 'orbs/:orbId', element: <OrbEditorPage /> },
          { path: 'context', element: <ContextPage /> },
          { path: 'models', element: <ModelsPage /> },
          { path: 'settings', element: <SettingsPage /> },
          { path: '*', element: <NotFound /> },
        ],
      },
    ],
  },
]

export function createQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { staleTime: 10_000, retry: 1 } } })
}

// The demo is static hosting without server-side routing, so it keeps the route in the URL hash.
const router = IS_DEMO ? createHashRouter(routes) : createBrowserRouter(routes)
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
