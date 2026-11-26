import { icon, REPO, BRANCH } from '../lib/layout.mjs'
import { orb } from '../lib/orb.mjs'

export function openSource({ root }) {
  const ways = [
    ['Add a model provider', 'Teach Orbs a new kind of model API. Providers are small, self-contained adapters.', 'docs/guides/adding-a-model-provider/', { color: 'plasma', eyes: 'visor', accessory: 'antenna', shape: 'circle' }],
    ['Add a context connector', 'Let orbs read a new kind of source. Connectors are one class each.', 'docs/guides/adding-a-context-connector/', { color: 'mint', eyes: 'happy', accessory: 'sprout', shape: 'squircle' }],
    ['Improve the docs', 'Found something confusing? A clearer sentence is a real contribution.', 'docs/', { color: 'sun', eyes: 'wide', accessory: 'halo', shape: 'blob' }],
    ['Report bugs and ideas', 'Steps to reproduce help most. Ideas start best in Discussions.', `${REPO}/issues/new/choose`, { color: 'bubblegum', eyes: 'wink', accessory: 'crown', shape: 'drop' }],
  ]
  const files = [
    ['LICENSE', 'MIT License, copyright Orbs contributors', 'license/'],
    ['README.md', 'What Orbs is and how to run it', `${REPO}#readme`],
    ['CONTRIBUTING.md', 'Setup, house rules and how to open a pull request', 'contributing/'],
    ['CODE_OF_CONDUCT.md', 'The Contributor Covenant and how to report', 'code-of-conduct/'],
    ['SECURITY.md', 'How to report a vulnerability privately', 'security/'],
    ['PRIVACY.md', 'What happens with your data', 'privacy/'],
    ['CHANGELOG.md', 'Every notable change, per release', 'changelog/'],
    ['docs/', 'Guides, arc42 architecture and decision records', 'docs/'],
  ]
  const href = (h) => (h.startsWith('http') ? h : root + h)

  return `
<div class="wrap page narrow-page">
  <div class="page-head">
    <span class="eyebrow">Open source</span>
    <h1 class="display-2">Built in the open, free for everyone</h1>
    <p class="lead">Orbs is MIT licensed. Here is what that means in plain words, and how you can help make it better.</p>
  </div>

  <section class="grid-3">
    <div class="card wash-mint borderless">
      <h2 class="h3">You can</h2>
      <ul class="checks">
        <li>${icon('check')}Use Orbs for anything, including commercial work</li>
        <li>${icon('check')}Change the code however you like</li>
        <li>${icon('check')}Share, fork and redistribute it</li>
        <li>${icon('check')}Build your own product on top of it</li>
      </ul>
    </div>
    <div class="card wash-plasma borderless">
      <h2 class="h3">You must</h2>
      <p>Keep the copyright notice and license text in copies of the code.</p>
      <p>That's it. If you publish a changed version, giving it its own name avoids confusion.</p>
    </div>
    <div class="card wash-sky borderless">
      <h2 class="h3">Good to know</h2>
      <p>Orbs comes without warranty, and the authors aren't liable for how it's used.</p>
      <a class="strong-link" href="${root}license/">Read the full license</a>
    </div>
  </section>

  <section class="stack">
    <h2 class="h2">Ways to help</h2>
    <div class="grid-4">
      ${ways
        .map(
          ([t, d, h, look]) =>
            `<a class="card link-card" href="${href(h)}">${orb({ ...look, mouth: 'smile', blush: false, glow: false, size: 52 })}<strong class="card-title">${t}</strong><span class="muted">${d}</span></a>`,
        )
        .join('')}
    </div>
  </section>

  <section class="grid-2 wide-cards">
    <div class="card">
      <h2 class="h3">Your first pull request</h2>
      <ol class="numbered">
        <li>Pick an issue labelled <span class="tag ok">good first issue</span>, or open one to propose your idea first.</li>
        <li>Fork the repository and follow the setup in <a href="${root}contributing/">CONTRIBUTING.md</a>.</li>
        <li>Run <code>make lint</code> and <code>make test</code>, and write commits as <a href="https://www.conventionalcommits.org">Conventional Commits</a>.</li>
        <li>Open the pull request. I review pull requests in my spare time, so it can take a while.</li>
      </ol>
    </div>
    <div id="conduct" class="card">
      <h2 class="h3">Be kind</h2>
      <p class="muted">Everyone taking part in Orbs follows the Code of Conduct, based on the Contributor Covenant. Be welcoming and assume good intent. Report problems privately and they're handled confidentially.</p>
      <a class="strong-link" href="${root}code-of-conduct/">Read the Code of Conduct</a>
    </div>
  </section>

  <section class="panel">
    <h2 class="h3">What's in the repository</h2>
    <div class="file-grid">
      ${files
        .map(([n, d, h]) => `<a class="file" href="${href(h)}"><span class="mono">${n}</span><span class="muted">${d}</span></a>`)
        .join('')}
    </div>
    <p class="muted small">Browse everything on <a href="${REPO}/tree/${BRANCH}">GitHub</a>.</p>
  </section>
</div>
`
}
