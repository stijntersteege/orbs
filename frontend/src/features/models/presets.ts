/**
 * Friendly starting points for common providers. Pure data: the server only knows
 * adapter kinds, so adding a preset never needs a backend change.
 */
export interface ProviderPreset {
  id: string
  name: string
  kind: string
  baseUrl?: string
  needsKey: boolean
  defaultModel?: string
  hint: string
}

export const PROVIDER_PRESETS: ProviderPreset[] = [
  {
    id: 'openrouter',
    name: 'OpenRouter',
    kind: 'openai-compatible',
    baseUrl: 'https://openrouter.ai/api/v1',
    needsKey: true,
    hint: 'One key for models from every major lab.',
  },
  {
    id: 'ollama',
    name: 'Ollama',
    kind: 'openai-compatible',
    baseUrl: 'http://localhost:11434/v1',
    needsKey: false,
    hint: 'Run models on your own machine. Private and free.',
  },
  {
    id: 'lmstudio',
    name: 'LM Studio',
    kind: 'openai-compatible',
    baseUrl: 'http://localhost:1234/v1',
    needsKey: false,
    hint: 'Local models with a desktop app.',
  },
  {
    id: 'openai',
    name: 'OpenAI',
    kind: 'openai-compatible',
    baseUrl: 'https://api.openai.com/v1',
    needsKey: true,
    hint: 'GPT models with your own key.',
  },
  {
    id: 'custom',
    name: 'Custom endpoint',
    kind: 'openai-compatible',
    needsKey: false,
    hint: 'Any OpenAI-compatible URL: vLLM, LiteLLM, your own gateway.',
  },
  {
    id: 'echo',
    name: 'Echo (demo)',
    kind: 'echo',
    needsKey: false,
    defaultModel: 'echo-1',
    hint: 'Repeats what you say. Try Orbs without a key.',
  },
]
