import { escapeHtml, icon, REPO } from '../lib/layout.mjs'
import { orb } from '../lib/orb.mjs'

const FAQ = [
  [
    'Is Orbs really free?',
    'Yes. Orbs is released under the MIT License, so you can use it for anything, including at work, change it and share it. You only pay your model provider for what your orbs use, or nothing if you run a model on your own machine.',
  ],
  [
    'Which AI models can I use?',
    'Anything that speaks the OpenAI chat API: OpenAI, OpenRouter (hundreds of models with one key), Ollama, LM Studio, vLLM, LiteLLM and more. Each orb can use its own model, or "Auto" picks one for it.',
  ],
  [
    'Where does my data go?',
    'It stays in the database on the machine that runs Orbs. When an orb answers, your message and the sources linked to that orb go only to the model provider you chose. Orbs itself sends nothing anywhere: no telemetry, no tracking.',
  ],
  [
    'Do I need to host it myself?',
    'Yes. Orbs runs on your laptop, a home server or any cloud machine with Docker, and it takes one command. On your phone, open your Orbs address and add it to your home screen. Want a look first? Try the live demo.',
  ],
  [
    'Can I use Orbs at work or in my own product?',
    'Yes. The MIT License allows commercial use. Keep the copyright notice and license text in copies of the code. If you publish a changed version, please give it its own name.',
  ],
  [
    'Who makes Orbs?',
    'Orbs is a personal hobby project by Stijn ter Steege, built in spare time. There is no company behind it and no fixed roadmap, but ideas, bug reports and pull requests are very welcome.',
  ],
]

export function home({ root }) {
  const steps = [
    ['1', '#C9B8FF', 'Connect a model', 'Paste an OpenRouter or OpenAI key, or point Orbs at a model on your own machine.'],
    ['2', '#2EE59D', 'Choose what it knows', 'Add notes and web pages, then switch on the ones this orb may read. Nothing else.'],
    ['3', '#FF7AC6', 'Give it a face', 'Pick a shape, eyes, mouth, color and a little accessory, name it and say hi.'],
  ]
  const privacy = [
    ['server', '#C9B8FF', 'Runs where you want', 'Your laptop, a home server or your own cloud. One container.'],
    ['lock', '#FFC53D', 'Keys stay encrypted', 'Provider keys are encrypted at rest and never sent back by the API.'],
    ['eye', '#2EE59D', 'Orbs see only what you allow', 'Each orb only sends its model the sources you linked to it.'],
    ['shield', '#4CC9FF', 'No tracking at all', 'No telemetry, no analytics, nothing phoning home.'],
  ]

  return `
<section class="hero wrap">
  <div class="hero-text">
    <a class="pill" href="${root}open-source/"><span class="pill-tag">MIT</span>Free and open source</a>
    <h1 class="display">Little orbs.<br />Big context.</h1>
    <p class="lead">Create AI companions that know your notes and web pages, run on any model you choose, and look exactly how you like. Free, MIT-licensed and yours to host.</p>
    <div class="actions">
      <a class="button primary large glow" href="${root}get/">Get Orbs, it's free</a>
      <a class="button ghost large" href="${root}demo/">Try the live demo ${icon('arrow')}</a>
    </div>
    <p class="fineprint">Works with OpenAI, OpenRouter, Ollama, LM Studio, vLLM, LiteLLM and any OpenAI-compatible endpoint.</p>
  </div>
  <div class="hero-art" aria-hidden="true">
    <div class="art-main">${orb({ size: 260, floating: true })}</div>
    <div class="art-a">${orb({ color: 'sun', shape: 'blob', eyes: 'wide', mouth: 'open', accessory: 'halo', blush: false, size: 130 })}</div>
    <div class="art-b">${orb({ color: 'mint', shape: 'squircle', eyes: 'visor', mouth: 'flat', accessory: 'sprout', blush: false, size: 140 })}</div>
    <div class="art-c">${orb({ color: 'coral', shape: 'drop', eyes: 'wink', mouth: 'grin', accessory: 'crown', blush: false, size: 120 })}</div>
    <div class="chat-card">
      <span class="chat-name">Pip <span class="mono muted">· 1 source</span></span>
      <span>An orb is a model, its context and a face. I can only read what you switch on for me.</span>
      <span class="chips"><span class="chip">● About Orbs</span><span class="chip">● echo-1</span></span>
    </div>
  </div>
</section>

<section id="features" class="wrap section">
  <div class="section-head">
    <span class="eyebrow">What makes an orb</span>
    <h2 class="h2">A brain, a memory and a face. You pick all three.</h2>
  </div>
  <div class="grid-3">
    <article class="feature wash-plasma">
      ${orb({ eyes: 'visor', blush: false, size: 84 })}
      <h3>Any model</h3>
      <p>Use one OpenRouter key for hundreds of models, your own OpenAI key, or a model on your own machine with Ollama or LM Studio. Switch any time. Orbs never lock you in.</p>
    </article>
    <article class="feature wash-mint">
      ${orb({ color: 'mint', shape: 'squircle', eyes: 'wide', mouth: 'open', accessory: 'sprout', blush: false, size: 84 })}
      <h3>Any context</h3>
      <p>Add notes and web pages once and share them with the orbs you pick. Each orb only sees what you switch on, and you can always see which sources it has.</p>
    </article>
    <article class="feature wash-sky">
      ${orb({ color: 'bubblegum', shape: 'drop', eyes: 'wink', mouth: 'grin', accessory: 'crown', size: 84 })}
      <h3>Any face</h3>
      <p>Pick a shape, eyes, mouth, color and a little hat. Thousands of combinations, so you can tell your work orb from your kitchen orb at a glance.</p>
    </article>
  </div>
</section>

<section id="how" class="band">
  <div class="wrap section split">
    <div class="split-text">
      <span class="eyebrow">How it works</span>
      <h2 class="h2">Your first orb in about a minute</h2>
      <ol class="steps">
        ${steps
          .map(
            ([n, c, t, d]) =>
              `<li><span class="step-num" style="background:${c}">${n}</span><span><strong>${t}</strong><span class="muted">${d}</span></span></li>`,
          )
          .join('')}
      </ol>
      <a class="button ghost" href="${root}demo/">Open the live demo full screen ${icon('arrow')}</a>
    </div>
    <div class="phone">
      <iframe src="${root}demo/#/orbs/demo-orb-pip/chat" title="Orbs live demo: chat with Pip" loading="lazy" data-follows-theme></iframe>
    </div>
  </div>
</section>

<section class="wrap section privacy">
  <div class="privacy-text">
    <span class="eyebrow">Privacy</span>
    <h2 class="h2">Yours, all the way down</h2>
    <p class="muted">Orbs runs on your own machine. Your chats, keys and context stay there, and only go to the model provider you pick. <a href="${root}privacy/">Read the privacy page</a>.</p>
  </div>
  <div class="grid-2">
    ${privacy
      .map(
        ([i, c, t, d]) =>
          `<div class="card small-card"><span class="icon-tile" style="background:${c}">${icon(i)}</span><strong>${t}</strong><span class="muted">${d}</span></div>`,
      )
      .join('')}
  </div>
</section>

<section class="wrap section-tight">
  <div class="cta-panel">
    <div class="cta-text">
      <h2 class="h2">MIT licensed. Fork it, ship it, make it weird.</h2>
      <p class="muted">Every line of Orbs is on GitHub. Use it at home or at work, change anything, build on it. Pull requests, new connectors and new orb faces are very welcome.</p>
      <div class="actions">
        <a class="button inverse" href="${REPO}">View on GitHub</a>
        <a class="button ghost" href="${root}open-source/">How to contribute</a>
      </div>
    </div>
    <pre class="terminal"><code><span class="t-dim"># run Orbs on your machine</span>
<span class="t-ok">$</span> docker run -p 8080:8080 \\
    -v orbs-data:/data ghcr.io/stijntersteege/orbs
<span class="t-dim"># then open</span>
<span class="t-link">http://localhost:8080</span></code></pre>
  </div>
</section>

<section class="narrow section-tight">
  <h2 class="h2 center">Questions, answered</h2>
  <div class="faq">
    ${FAQ.map(
      ([q, a], i) =>
        `<details class="card"${i === 0 ? ' open' : ''}><summary>${escapeHtml(q)}<span class="faq-sign" aria-hidden="true"></span></summary><p class="muted">${escapeHtml(a)}</p></details>`,
    ).join('')}
  </div>
</section>

<section class="final-cta">
  ${orb({ color: 'sky', eyes: 'happy', mouth: 'open', accessory: 'headphones', size: 120, floating: true })}
  <h2 class="h2">Who will your first orb be?</h2>
  <a class="button primary large glow" href="${root}get/">Get Orbs, it's free</a>
</section>
`
}
