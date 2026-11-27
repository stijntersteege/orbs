import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { memoriesApi } from '@/api/endpoints'
import { keys, useMemories } from '@/api/queries'
import type { Memory, Orb } from '@/api/types'
import { Button, ErrorNotice } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import styles from './chat.module.css'

/** What the orb remembers. Every memory can be read, changed and forgotten. */
export function MemoryTab({ orb }: { orb: Orb }) {
  const { data: memories, error } = useMemories(orb.id)
  const client = useQueryClient()
  const refresh = () => client.invalidateQueries({ queryKey: keys.memories(orb.id) })
  const [draft, setDraft] = useState('')
  const [shareNew, setShareNew] = useState(false)
  const add = useMutation({
    mutationFn: () => memoriesApi.add(orb.id, draft, shareNew),
    onSuccess: () => {
      setDraft('')
      setShareNew(false)
      void refresh()
    },
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (draft.trim()) add.mutate()
  }

  return (
    <div className={styles.memory}>
      <ErrorNotice error={error ?? add.error} />
      {memories?.length === 0 && (
        <p className={styles.activityEmpty}>
          {orb.name} remembers nothing yet. It saves facts it learns in chats, or you can add them here.
        </p>
      )}
      <ul className={styles.memoryList} aria-label={`What ${orb.name} remembers`}>
        {memories?.map((m) => (
          <MemoryItem key={m.id} memory={m} orbId={orb.id} onChange={refresh} />
        ))}
      </ul>
      <form onSubmit={submit} className={styles.memoryForm} aria-label="Add a memory">
        <input
          className={styles.memoryInput}
          value={draft}
          maxLength={1000}
          placeholder="Something to remember…"
          aria-label="New memory"
          onChange={(e) => setDraft(e.target.value)}
        />
        <Button small type="submit" disabled={add.isPending || !draft.trim()}>
          Add
        </Button>
      </form>
      <label className={styles.memoryShare}>
        <input type="checkbox" checked={shareNew} onChange={(e) => setShareNew(e.target.checked)} />
        Share new memories with all orbs
      </label>
    </div>
  )
}

function MemoryItem({ memory, orbId, onChange }: { memory: Memory; orbId: string; onChange: () => void }) {
  const [editing, setEditing] = useState(false)
  const [text, setText] = useState(memory.content)
  const save = useMutation({
    mutationFn: () => memoriesApi.edit(memory.id, text),
    onSuccess: () => {
      setEditing(false)
      onChange()
    },
  })
  const forget = useMutation({ mutationFn: () => memoriesApi.forget(memory.id), onSuccess: onChange })
  const share = useMutation({
    mutationFn: () => memoriesApi.edit(memory.id, memory.content, !memory.shared, orbId),
    onSuccess: onChange,
  })

  return (
    <li className={styles.memoryItem}>
      {editing ? (
        <form
          className={styles.memoryForm}
          onSubmit={(e) => {
            e.preventDefault()
            save.mutate()
          }}
        >
          <input
            className={styles.memoryInput}
            value={text}
            aria-label="Edit memory"
            onChange={(e) => setText(e.target.value)}
          />
          <Button small type="submit" disabled={save.isPending}>
            Save
          </Button>
        </form>
      ) : (
        <>
          <span className={styles.memoryText}>
            {memory.content}
            {memory.source === 'learned' && <span className={styles.actDetail}> · learned in a chat</span>}
            {memory.shared && <span className={styles.actDetail}> · all orbs</span>}
          </span>
          <button
            type="button"
            className={styles.panelClose}
            aria-label={
              memory.shared
                ? `Keep "${memory.content}" for this orb only`
                : `Share "${memory.content}" with all orbs`
            }
            title={memory.shared ? 'Keep for this orb only' : 'Share with all orbs'}
            onClick={() => share.mutate()}
          >
            <Icon name={memory.shared ? 'user' : 'users'} size={16} />
          </button>
          <button
            type="button"
            className={styles.panelClose}
            aria-label={`Edit "${memory.content}"`}
            onClick={() => setEditing(true)}
          >
            <Icon name="edit" size={16} />
          </button>
          <button
            type="button"
            className={styles.panelClose}
            aria-label={`Forget "${memory.content}"`}
            onClick={() => forget.mutate()}
          >
            <Icon name="trash" size={16} />
          </button>
        </>
      )}
    </li>
  )
}
