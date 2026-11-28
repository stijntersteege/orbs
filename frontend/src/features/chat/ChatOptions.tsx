import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { conversationsApi } from '@/api/endpoints'
import type { ConversationSummary } from '@/api/types'
import { Icon } from '@/components/ui/icons'
import { usePopover } from '@/lib/usePopover'
import styles from './chat.module.css'

/** Renames or deletes a chat. Used in the chat header and on the list of all chats. */
export function useChatActions(orbId: string) {
  const client = useQueryClient()
  const refresh = () => {
    void client.invalidateQueries({ queryKey: ['conversations'] })
    void client.invalidateQueries({ queryKey: ['conversation'] })
  }
  const rename = useMutation({
    mutationFn: ({ id, title }: { id: string; title: string }) => conversationsApi.rename(id, title),
    onSuccess: refresh,
  })
  const remove = useMutation({
    mutationFn: (id: string) => conversationsApi.remove(id),
    onSuccess: refresh,
  })
  const confirmRemove = async (chat: Pick<ConversationSummary, 'id' | 'title'>) => {
    if (!window.confirm(`Delete "${chat.title}"? This cannot be undone.`)) return false
    await remove.mutateAsync(chat.id)
    return true
  }
  return { rename, remove, confirmRemove, orbId }
}

/** A small form to type a new chat title. */
export function RenameChat({
  title,
  onSave,
  onCancel,
  busy,
}: {
  title: string
  onSave: (title: string) => void
  onCancel: () => void
  busy?: boolean
}) {
  const [value, setValue] = useState(title)
  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (value.trim()) onSave(value.trim())
  }
  return (
    <form className={styles.renameForm} onSubmit={submit} aria-label="Rename chat">
      <input
        className={styles.renameInput}
        aria-label="Chat name"
        value={value}
        maxLength={120}
        required
        onChange={(e) => setValue(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Escape') onCancel()
        }}
      />
      <button type="submit" className={styles.renameSave} disabled={busy || !value.trim()}>
        Save
      </button>
      <button type="button" className={styles.renameCancel} onClick={onCancel}>
        Cancel
      </button>
    </form>
  )
}

/** The "more" menu for the open chat: rename it or delete it. */
export function ChatOptions({
  orbId,
  chat,
}: {
  orbId: string
  chat: Pick<ConversationSummary, 'id' | 'title'>
}) {
  const popover = usePopover()
  const navigate = useNavigate()
  const actions = useChatActions(orbId)
  const [renaming, setRenaming] = useState(false)

  return (
    <div className={styles.popoverWrap} ref={popover.ref}>
      <button
        type="button"
        className={styles.headerButton}
        aria-expanded={popover.open}
        aria-haspopup="true"
        aria-label="Chat options"
        onClick={() => {
          setRenaming(false)
          popover.toggle()
        }}
      >
        <Icon name="more" size={16} />
      </button>
      {popover.open && (
        <div className={[styles.popover, styles.alignEnd, styles.below].join(' ')}>
          <div className={styles.orbMenu}>
            {renaming ? (
              <RenameChat
                title={chat.title}
                busy={actions.rename.isPending}
                onCancel={() => setRenaming(false)}
                onSave={(title) =>
                  actions.rename.mutate(
                    { id: chat.id, title },
                    {
                      onSuccess: () => {
                        setRenaming(false)
                        popover.close()
                      },
                    },
                  )
                }
              />
            ) : (
              <>
                <span className={styles.modelGroupLabel}>{chat.title}</span>
                <button type="button" className={styles.modelOption} onClick={() => setRenaming(true)}>
                  <Icon name="edit" size={16} />
                  <span className={styles.modelName}>Rename chat</span>
                </button>
                <button
                  type="button"
                  className={[styles.modelOption, styles.danger].join(' ')}
                  onClick={async () => {
                    if (await actions.confirmRemove(chat)) {
                      popover.close()
                      void navigate(`/orbs/${orbId}/chat`)
                    }
                  }}
                >
                  <Icon name="trash" size={16} />
                  <span className={styles.modelName}>Delete chat</span>
                </button>
              </>
            )}
            {(actions.rename.error ?? actions.remove.error) && (
              <span className={styles.actDetail}>
                {(actions.rename.error ?? actions.remove.error)!.message}
              </span>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
