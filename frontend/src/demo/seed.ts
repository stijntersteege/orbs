import type { ContextSource, Conversation, Memory, Orb, Provider, Skill, User } from '@/api/types'
import { DEFAULT_APPEARANCE } from '@/api/types'

export interface DemoState {
  orbs: Orb[]
  providers: Provider[]
  contextSources: ContextSource[]
  conversations: Conversation[]
  /** The demo account, once its name was changed. */
  user?: User
  memories?: Memory[]
  skills?: Skill[]
}

const ABOUT_ORBS = `Orbs is an open-source home for AI companions.
An orb is a model, its context and a face.
Connect any model provider under Models, add context under Context,
and give every orb its own look in the orb editor.
The demo provider "Echo" repeats what you say, so you can try Orbs without an API key.`

const GROCERIES = `Oat milk, blueberries, rye bread, coffee beans, basil, lemons.`

/** Sample data for the live demo: a few orbs with different faces and one earlier chat. */
export function seed(now: Date): DemoState {
  const at = (minutesAgo: number) => new Date(now.getTime() - minutesAgo * 60_000).toISOString()

  const echo: Provider = {
    id: 'demo-provider-echo',
    name: 'Echo (demo)',
    kind: 'echo',
    hasApiKey: false,
    defaultModel: 'echo-1',
    isEnabled: true,
    createdAt: at(600),
    updatedAt: at(600),
  }

  const note = (id: string, name: string, input: string, minutesAgo: number): ContextSource => ({
    id,
    name,
    kind: 'text',
    location: '',
    input,
    status: 'ready',
    lastSyncedAt: at(minutesAgo),
    contentLength: input.length,
    createdAt: at(minutesAgo),
    updatedAt: at(minutesAgo),
  })
  const about = note('demo-source-about', 'About Orbs', ABOUT_ORBS, 590)
  const groceries = note('demo-source-groceries', 'Grocery list', GROCERIES, 300)

  const orb = (id: string, fields: Partial<Orb> & Pick<Orb, 'name'>, minutesAgo: number): Orb => ({
    id,
    description: '',
    instructions: '',
    appearance: DEFAULT_APPEARANCE,
    contextSourceIds: [],
    createdAt: at(minutesAgo),
    updatedAt: at(minutesAgo),
    ...fields,
  })

  const pip = orb(
    'demo-orb-pip',
    {
      name: 'Pip',
      description: 'A friendly guide to Orbs',
      instructions: 'Lead with the answer, keep it short and friendly.',
      contextSourceIds: [about.id],
    },
    580,
  )
  const nova = orb(
    'demo-orb-nova',
    {
      name: 'Nova',
      description: 'Late-night brainstorm buddy',
      instructions: 'Offer three wild ideas before one sensible one.',
      appearance: {
        ...DEFAULT_APPEARANCE,
        color: 'ocean',
        shape: 'blob',
        eyes: 'wide',
        mouth: 'grin',
        accessory: 'halo',
      },
      model: { providerId: echo.id, model: 'echo-1' },
    },
    400,
  )
  const basil = orb(
    'demo-orb-basil',
    {
      name: 'Basil',
      description: 'Keeps the kitchen running',
      instructions: 'Suggest recipes that use what is on the grocery list.',
      appearance: {
        ...DEFAULT_APPEARANCE,
        color: 'mint',
        shape: 'squircle',
        eyes: 'sleepy',
        mouth: 'smile',
        accessory: 'sprout',
        glow: false,
      },
      contextSourceIds: [groceries.id],
    },
    290,
  )

  const chat: Conversation = {
    id: 'demo-conversation-hello',
    orbId: pip.id,
    title: 'What is an orb?',
    createdAt: at(30),
    updatedAt: at(29),
    messages: [
      { id: 'demo-message-1', role: 'user', content: 'What is an orb?', createdAt: at(30) },
      {
        id: 'demo-message-2',
        role: 'assistant',
        content: 'You said: What is an orb?',
        createdAt: at(29),
      },
    ],
  }

  return {
    orbs: [pip, nova, basil],
    providers: [echo],
    contextSources: [about, groceries],
    conversations: [chat],
  }
}
