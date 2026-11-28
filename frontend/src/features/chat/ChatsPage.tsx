import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useConversations, useOrb } from '@/api/queries'
import type { ConversationSummary } from '@/api/types'
import { Button, ButtonLink, Card, ErrorNotice, PageHeader, Spinner } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import shared from '../shared.module.css'
import { RenameChat, useChatActions } from './ChatOptions'

const when = (iso: string) =>
  new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })

/** Every chat with one orb: open, rename or delete them. */
export function ChatsPage() {
  const { orbId } = useParams()
  const orb = useOrb(orbId)
  const chats = useConversations(orbId)
  const name = orb.data?.name ?? 'this orb'

  return (
    <>
      <PageHeader
        title={`Chats with ${name}`}
        subtitle="Open, rename or delete your chats. Deleting a chat cannot be undone."
        actions={
          <ButtonLink to={`/orbs/${orbId}/chat`} variant="primary">
            <Icon name="plus" size={18} /> New chat
          </ButtonLink>
        }
      />
      <div className={shared.stack} style={{ maxWidth: 760 }}>
        <Card>
          {chats.isPending && <Spinner />}
          <ErrorNotice error={chats.error ?? orb.error} />
          {chats.data?.length === 0 && (
            <p className={shared.muted}>No chats yet. Start one and it shows up here.</p>
          )}
          <ul className={shared.list} aria-label="Chats">
            {chats.data?.map((c) => (
              <ChatRow key={c.id} chat={c} orbId={orbId!} />
            ))}
          </ul>
        </Card>
      </div>
    </>
  )
}

function ChatRow({ chat, orbId }: { chat: ConversationSummary; orbId: string }) {
  const actions = useChatActions(orbId)
  const [renaming, setRenaming] = useState(false)
  return (
    <li className={shared.row}>
      <span className={shared.rowText}>
        {renaming ? (
          <RenameChat
            title={chat.title}
            busy={actions.rename.isPending}
            onCancel={() => setRenaming(false)}
            onSave={(title) =>
              actions.rename.mutate({ id: chat.id, title }, { onSuccess: () => setRenaming(false) })
            }
          />
        ) : (
          <Link to={`/orbs/${orbId}/chat/${chat.id}`}>
            <strong>{chat.title}</strong>
          </Link>
        )}
        <span className={shared.muted}>Last message {when(chat.updatedAt)}</span>
        <ErrorNotice error={actions.rename.error ?? actions.remove.error} />
      </span>
      {!renaming && (
        <Button small onClick={() => setRenaming(true)}>
          Rename
        </Button>
      )}
      <Button
        small
        variant="ghost"
        aria-label={`Delete ${chat.title}`}
        onClick={() => void actions.confirmRemove(chat)}
        disabled={actions.remove.isPending}
      >
        <Icon name="trash" />
      </Button>
    </li>
  )
}
