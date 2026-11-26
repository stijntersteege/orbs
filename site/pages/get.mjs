import { icon, REPO } from '../lib/layout.mjs'
import { orb } from '../lib/orb.mjs'

const METHODS = [
  [
    'compose',
    'Docker Compose',
    `<span class="t-dim"># 1. Get the code and start Orbs with PostgreSQL</span>
<span class="t-ok">$</span> git clone https://github.com/stijntersteege/orbs.git && cd orbs
<span class="t-ok">$</span> cp .env.example .env   <span class="t-dim"># optional: change the password, set an API token</span>
<span class="t-ok">$</span> docker compose up -d
<span class="t-dim"># 2. Open it in your browser</span>
<span class="t-link">http://localhost:8080</span>`,
  ],
  [
    'container',
    'One container',
    `<span class="t-dim"># 1. Start Orbs with SQLite (data is kept in the "orbs-data" volume)</span>
<span class="t-ok">$</span> docker run -d --name orbs -p 8080:8080 -v orbs-data:/data ghcr.io/stijntersteege/orbs
<span class="t-dim"># 2. Open it in your browser</span>
<span class="t-link">http://localhost:8080</span>`,
  ],
  [
    'source',
    'From source',
    `<span class="t-dim"># Needs the .NET 10 SDK and Node.js 24 with pnpm</span>
<span class="t-ok">$</span> git clone https://github.com/stijntersteege/orbs.git && cd orbs
<span class="t-ok">$</span> make setup
<span class="t-ok">$</span> make dev-api   <span class="t-dim"># terminal 1: API on http://localhost:5080</span>
<span class="t-ok">$</span> make dev-web   <span class="t-dim"># terminal 2: web app on http://localhost:5173</span>`,
  ],
]

export function get({ root }) {
  const steps = [
    ['1', '#C9B8FF', 'Say hi to Pip', 'A demo orb called Pip and the Echo model are ready, so you can try everything without a key.'],
    ['2', '#2EE59D', 'Connect a model', 'Under Models, add an OpenRouter or OpenAI key, or a local model from Ollama or LM Studio.'],
    ['3', '#FF7AC6', 'Make your first orb', 'Choose what it knows, give it a face and start chatting.'],
  ]
  const tabs = METHODS.map(
    ([id, label], i) =>
      `<input type="radio" name="install" id="install-${id}" class="tab-input"${i === 0 ? ' checked' : ''} /><label for="install-${id}" class="tab">${label}</label>`,
  ).join('')
  const panels = METHODS.map(
    ([id, , code]) => `<pre class="terminal tab-panel" data-for="install-${id}"><code>${code}</code></pre>`,
  ).join('')

  // CSS-only tabs: without :has() support, every install method simply shows.
  const tabCss = METHODS.map(
    ([id]) => `.tabs:has(#install-${id}:checked) .tab-panel[data-for="install-${id}"]{display:block}`,
  ).join('')

  return `
<style>.tabs:has(.tab-input:checked) .tab-panel{display:none}${tabCss}</style>
<div class="wrap page">
  <div class="page-hero">
    <div>
      <h1 class="display-2">Get Orbs running in two minutes</h1>
      <p class="lead">Orbs runs on your laptop, a home server or any cloud machine. Pick how you'd like to install it.</p>
    </div>
    ${orb({ color: 'mint', shape: 'blob', eyes: 'happy', mouth: 'open', accessory: 'sprout', size: 150, floating: true })}
  </div>

  <section class="tabs" aria-label="Install method">
    <div class="tab-list">${tabs}</div>
    ${panels}
  </section>

  <div class="grid-3">
    ${steps
      .map(
        ([n, c, t, d]) =>
          `<div class="card"><span class="step-num" style="background:${c}">${n}</span><strong class="card-title">${t}</strong><span class="muted">${d}</span></div>`,
      )
      .join('')}
  </div>

  <div class="grid-2 wide-cards">
    <section class="card">
      <h2 class="h3">What you need</h2>
      <ul class="checks">
        <li>${icon('check')}A computer or server that runs Docker</li>
        <li>${icon('check')}A model: an API key from OpenRouter, OpenAI or another OpenAI-compatible provider, or Ollama or LM Studio for local models</li>
        <li>${icon('check')}HTTPS and an API token if Orbs is reachable from the internet</li>
      </ul>
      <p class="muted small">Running models locally needs more memory, depending on the model. The <a href="${root}docs/guides/self-hosting/">self-hosting guide</a> covers backups, HTTPS and updates.</p>
    </section>
    <section id="mobile" class="card wash-sky borderless">
      <h2 class="h3">Take your orbs along</h2>
      <p class="muted">Open your Orbs address on your phone and add it to your home screen. You can also build the iOS and Android apps yourself; they connect to your own server.</p>
      <a class="button inverse" href="${root}docs/guides/mobile/">Mobile guide</a>
    </section>
  </div>

  <section class="help-row">
    <div>
      <strong>Stuck?</strong>
      <span class="muted">Orbs is a hobby project, so answers can take a while, but questions are welcome.</span>
    </div>
    <div class="actions">
      <a class="button ghost" href="${root}docs/">Read the docs</a>
      <a class="button ghost" href="${REPO}/discussions">Ask in Discussions</a>
      <a class="button ghost" href="${REPO}/issues/new/choose">Report a bug</a>
    </div>
  </section>
</div>
`
}
