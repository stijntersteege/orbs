import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { skillsApi } from '@/api/endpoints'
import { keys, useSkills } from '@/api/queries'
import type { Skill } from '@/api/types'
import { Button, Card, ErrorNotice, PageHeader, Spinner, TextArea, TextField } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import shared from '../shared.module.css'

/** Saves text as a file in the browser's downloads. */
function saveText(fileName: string, text: string) {
  const link = document.createElement('a')
  link.href = URL.createObjectURL(new Blob([text], { type: 'text/markdown' }))
  link.download = fileName
  link.click()
  URL.revokeObjectURL(link.href)
}

/** Routines every orb can follow. Orbs read them with the use_skill tool. */
export function SkillsPage() {
  const skills = useSkills()
  const client = useQueryClient()
  const refresh = () => client.invalidateQueries({ queryKey: keys.skills })
  const [editing, setEditing] = useState<Skill | null>(null)

  const importFile = useMutation({
    mutationFn: async (file: File) => skillsApi.import(await file.text()),
    onSuccess: () => void refresh(),
  })

  return (
    <>
      <PageHeader
        title="Skills"
        subtitle="Routines every orb can follow. Save the steps of a good reply from the activity panel, write one here, or import a SKILL.md file."
      />
      <div className={shared.stack} style={{ maxWidth: 760 }}>
        <Card>
          {skills.isPending && <Spinner />}
          <ErrorNotice error={skills.error} />
          {skills.data?.length === 0 && <p className={shared.muted}>No skills yet.</p>}
          <ul className={shared.list} aria-label="Skills">
            {skills.data?.map((s) => (
              <SkillRow key={s.id} skill={s} onEdit={() => setEditing(s)} onChange={refresh} />
            ))}
          </ul>
        </Card>

        <SkillForm
          key={editing?.id ?? 'new'}
          skill={editing}
          onDone={() => {
            setEditing(null)
            void refresh()
          }}
        />

        <Card>
          <div className={shared.stack}>
            <h2>Import</h2>
            <label className={shared.muted}>
              Add a SKILL.md file. A skill with the same name is replaced.{' '}
              <input
                type="file"
                aria-label="SKILL.md file"
                accept=".md,.markdown,text/markdown"
                onChange={(e) => {
                  const file = e.target.files?.[0]
                  if (file) importFile.mutate(file)
                  e.target.value = ''
                }}
              />
            </label>
            {importFile.isSuccess && <p className={shared.muted}>Imported {importFile.data.name}.</p>}
            <ErrorNotice error={importFile.error} />
          </div>
        </Card>
      </div>
    </>
  )
}

function SkillRow({ skill, onEdit, onChange }: { skill: Skill; onEdit: () => void; onChange: () => void }) {
  const remove = useMutation({ mutationFn: () => skillsApi.remove(skill.id), onSuccess: onChange })
  const download = useMutation({
    mutationFn: () => skillsApi.export(skill.id),
    onSuccess: (text) => saveText(`${skill.name}.SKILL.md`, text),
  })
  return (
    <li className={shared.row}>
      <span className={shared.rowText}>
        <strong className="mono">{skill.name}</strong>
        <span className={shared.muted}>{skill.description}</span>
        <ErrorNotice error={remove.error ?? download.error} />
      </span>
      <Button small onClick={onEdit}>
        Edit
      </Button>
      <Button small variant="ghost" onClick={() => download.mutate()} disabled={download.isPending}>
        Export
      </Button>
      <Button small variant="ghost" aria-label={`Delete ${skill.name}`} onClick={() => remove.mutate()}>
        <Icon name="trash" />
      </Button>
    </li>
  )
}

function SkillForm({ skill, onDone }: { skill: Skill | null; onDone: () => void }) {
  const [name, setName] = useState(skill?.name ?? '')
  const [description, setDescription] = useState(skill?.description ?? '')
  const [instructions, setInstructions] = useState(skill?.instructions ?? '')

  const save = useMutation({
    mutationFn: () => {
      const body = { name, description, instructions }
      return skill ? skillsApi.update(skill.id, body) : skillsApi.create(body)
    },
    onSuccess: () => {
      setName('')
      setDescription('')
      setInstructions('')
      onDone()
    },
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    save.mutate()
  }

  return (
    <Card>
      <form
        className={shared.stack}
        onSubmit={submit}
        aria-label={skill ? `Edit ${skill.name}` : 'New skill'}
      >
        <h2>{skill ? `Edit ${skill.name}` : 'New skill'}</h2>
        <TextField
          label="Name"
          required
          value={name}
          placeholder="weekly-review"
          hint="Lower case letters, digits and '-'. Orbs ask for the skill by this name."
          onChange={(e) => setName(e.target.value)}
        />
        <TextField
          label="When to use it"
          required
          value={description}
          placeholder="Sum up the person's week from their calendar and email."
          onChange={(e) => setDescription(e.target.value)}
        />
        <TextArea
          label="Steps"
          required
          rows={8}
          value={instructions}
          placeholder={
            "1. Read this week's calendar.\n2. Check the inbox for open questions.\n3. Write a short summary."
          }
          onChange={(e) => setInstructions(e.target.value)}
        />
        <ErrorNotice error={save.error} />
        <div className={shared.actions}>
          <Button type="submit" variant="primary" disabled={save.isPending}>
            {skill ? 'Save skill' : 'Add skill'}
          </Button>
          {skill && (
            <Button variant="ghost" onClick={onDone}>
              Cancel
            </Button>
          )}
        </div>
      </form>
    </Card>
  )
}
