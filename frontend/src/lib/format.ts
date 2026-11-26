/** "1 orb", "3 orbs". English-only for now; swap for Intl.PluralRules when Orbs gets translations. */
export function plural(count: number, singular: string, pluralForm = `${singular}s`): string {
  return `${count} ${count === 1 ? singular : pluralForm}`
}
