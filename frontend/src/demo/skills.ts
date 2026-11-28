import type { Message, Skill } from '@/api/types'

/** The demo's copy of the server's SKILL.md handling: front matter with a name and a description, then Markdown. */
export function parseSkill(markdown: string): { name: string; description: string; instructions: string } {
  const text = markdown.replace(/\r\n/g, '\n').replace(/^﻿/, '')
  const match = /^---[ \t]*\n([\s\S]*?)\n---[ \t]*(\n|$)/.exec(text)
  if (!match)
    throw new Error(
      'A SKILL.md file starts with front matter between --- lines, with a name and a description.',
    )

  const values: Record<string, string> = {}
  let folding: string | null = null
  let folded: string[] = []
  const endFold = () => {
    if (folding && folded.length) values[folding] = folded.join(' ')
    folding = null
    folded = []
  }
  for (const line of match[1]!.split('\n')) {
    const pair = /^([A-Za-z_][\w-]*):(.*)$/.exec(line)
    if (pair) {
      endFold()
      const value = pair[2]!.trim()
      if (['>', '|', '>-', '|-', ''].includes(value)) folding = pair[1]!
      else values[pair[1]!] = unquote(value)
    } else if (folding && line.startsWith(' ')) {
      folded.push(line.trim())
    }
  }
  endFold()

  const name = values.name?.trim()
  const description = values.description?.trim()
  if (!name || !description)
    throw new Error('A SKILL.md file needs a name and a description in its front matter.')
  const instructions = text.slice(match[0].length).trim()
  return { name, description, instructions: instructions || description }
}

export function renderSkill(skill: Pick<Skill, 'name' | 'description' | 'instructions'>): string {
  const description = skill.description.replace(/\n/g, ' ').trim().replace(/\\/g, '\\\\').replace(/"/g, '\\"')
  return `---\nname: ${skill.name}\ndescription: "${description}"\n---\n\n${skill.instructions.trim()}\n`
}

/** Lower case, words joined by '-'. */
export function skillName(title: string): string {
  return title
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 64)
    .replace(/-+$/, '')
}

/** The steps a reply took, as skill instructions. */
export function stepsFrom(messages: Message[], index: number): string | null {
  const reply = messages[index]
  const calls = (reply?.toolCalls ?? []).filter((c) => !c.isError && c.decision !== 'denied')
  if (reply?.role !== 'assistant' || calls.length === 0) return null
  const asked = messages
    .slice(0, index)
    .reverse()
    .find((m) => m.role === 'user')?.content
  const lines = asked ? [`This worked when the person asked: "${clip(asked, 300)}"`, ''] : []
  lines.push('Steps:', '')
  calls.forEach((c, i) => {
    const args = c.arguments.length > 2 ? ` with arguments like \`${clip(c.arguments, 500)}\`` : ''
    lines.push(`${i + 1}. Use the \`${c.tool}\` tool${args}.`)
  })
  lines.push('', 'Change the arguments to fit the new request, then answer with what you found.')
  return lines.join('\n')
}

const clip = (text: string, max: number) => (text.length <= max ? text : `${text.slice(0, max - 3)}...`)

function unquote(value: string): string {
  if (value.length >= 2 && value.startsWith('"') && value.endsWith('"'))
    return value.slice(1, -1).replace(/\\"/g, '"').replace(/\\\\/g, '\\')
  if (value.length >= 2 && value.startsWith("'") && value.endsWith("'"))
    return value.slice(1, -1).replace(/''/g, "'")
  return value
}
