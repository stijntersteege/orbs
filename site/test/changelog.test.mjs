import assert from 'node:assert/strict'
import { test } from 'node:test'
import { prepareChangelog } from '../build.mjs'

const release = '## [0.1.0] - 2026-11-26\n\n### Added\n\n- First release.\n\n[0.1.0]: https://example.test\n'

test('hides an empty Unreleased section', () => {
  const result = prepareChangelog(`# Changelog\n\n## [Unreleased]\n\n${release}`)
  assert.doesNotMatch(result, /Unreleased/)
  assert.match(result, /## \[0\.1\.0\]/)
})

test('keeps an Unreleased section that has entries', () => {
  const markdown = `# Changelog\n\n## [Unreleased]\n\n### Fixed\n\n- A bug.\n\n${release}`
  assert.equal(prepareChangelog(markdown), markdown)
})

test('hides an empty Unreleased section right before the link references', () => {
  const result = prepareChangelog('# Changelog\n\n## [Unreleased]\n\n[Unreleased]: https://example.test\n')
  assert.doesNotMatch(result, /^## \[Unreleased\]/m)
})
