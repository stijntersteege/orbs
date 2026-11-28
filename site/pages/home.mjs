import { escapeHtml, icon, REPO } from '../lib/layout.mjs'
import { orb } from '../lib/orb.mjs'

const FAQ = [
  [
    'Is Orbs really free?',
    'Yes. Orbs is MIT licensed: use it for anything, including at work. You only pay your model provider, or nothing with local models.',
  ],
  [
    'Which models can I use?',
    'OpenAI, Anthropic (Claude) and Google Gemini natively, OpenRouter with hundreds of models on one key, local models via Ollama or LM Studio, and anything else that speaks the OpenAI chat API.',
  ],
  [
    'Can my orbs act for me?',
    'Yes, with tools: web search, your calendar, email, Home Assistant and any MCP server. Anything that changes something asks you first, per orb, and every call is shown with the reply.',
  ],
  [
    'Can my family or team use it?',
    'Yes. Everyone gets their own account with a password, a passkey or single sign-on, and sees only their own orbs, models and chats.',
  ],
  [
    'Where does my data go?',
    'It stays on the machine running Orbs. Your questions and the sources linked to an orb go only to the model provider you picked for that orb. No telemetry, no tracking.',
  ],
  [
    'Do I need to self-host?',
    'Yes, for now. One Docker command on a laptop, home server or cloud machine. Want a look first? Try the live demo.',
  ],
  [
    'Can I build a product on it?',
    'Yes. Keep the license notice, and give your version its own name and logo.',
  ],
]

// Small orbs used in lists. Same options as the app's appearance fields.
const mini = (color, eyes, shape, size = 30) =>
  orb({ color, eyes, shape, accessory: 'none', blush: false, glow: false, size })

const features = [
  ['Tools that ask first', 'Web search, calendar, email, Home Assistant and any MCP server. Allow, ask first or block, per orb.'],
  ['Memory', 'Orbs keep the facts that matter, for one orb or all of them. See and edit everything they remember.'],
  ['Voice', 'Talk to an orb and hear it answer, each in its own voice.'],
  ['Schedules', 'A weekly summary, a feed watcher, a reply to new email: orbs work on their own and let you know.'],
  ['Channels', 'Chat with your orbs in Telegram, Matrix, Discord, Signal or email.'],
  ['Group chats', 'Mention another orb with @ and let your specialists answer together.'],
  ['Skills', 'Save a routine once and every orb can follow it. Works with SKILL.md files.'],
  ['Accounts', 'Passwords, passkeys or single sign-on. Everyone gets their own orbs.'],
]

export function home({ root }) {
  const roster = [
    ['plasma', 'happy', 'circle', 'Pip', 'Work sidekick', '#2EE59D', true],
    ['mint', 'visor', 'squircle', 'Byte', 'Explains the docs', '#2EE59D'],
    ['sun', 'wide', 'blob', 'Sunny', 'Recipes and groceries', '#FFC53D'],
    ['sky', 'sleepy', 'circle', 'Echo', 'Study buddy', '#2EE59D'],
    ['bubblegum', 'wink', 'drop', 'Inky', 'Writes like you', '#2EE59D'],
  ]
  const activity = [
    ['✓', 'Read the Roadmap note', 'Note · just now', 'ok'],
    ['✓', 'Read docs.acme.dev', 'Web page · just now', 'ok'],
    ['✓', 'Answered with claude-opus-5-5', 'OpenRouter · 2.1 s', 'ok'],
    ['·', 'Sources shown with the answer', 'Always on', 'dim'],
  ]
  const jobs = [
    ['plasma', 'Project sidekick', 'Knows the roadmap and your notes. Writes your status updates.', { eyes: 'happy', accessory: 'antenna', shape: 'circle', blush: true }, 1],
    ['mint', 'Docs explainer', 'Reads your docs site and answers in plain words.', { eyes: 'visor', mouth: 'flat', accessory: 'sprout', shape: 'squircle', blush: false }, 2],
    ['sun', 'Kitchen buddy', 'Plans dinners from what’s in the fridge and your recipes.', { eyes: 'wide', mouth: 'open', accessory: 'halo', shape: 'blob', blush: false }, 3],
    ['sky', 'Study buddy', 'Quizzes you on your notes until it sticks.', { eyes: 'sleepy', accessory: 'headphones', shape: 'circle', blush: true }, 4],
    ['bubblegum', 'Ghostwriter', 'Drafts posts and emails in your voice, from your notes.', { eyes: 'wink', mouth: 'grin', accessory: 'crown', shape: 'drop', blush: false }, 5],
    ['coral', 'Support helper', 'Answers customers from your docs and help pages.', { eyes: 'dots', accessory: 'headphones', shape: 'squircle', blush: false }, 5],
    ['ocean', 'Research scout', 'Reads the pages you add and shows where each answer came from.', { eyes: 'wide', accessory: 'ring', shape: 'circle', blush: false }, 1],
    ['mint', 'Meeting prep', 'Turns your meeting notes into a short brief.', { eyes: 'happy', accessory: 'none', shape: 'blob', blush: true }, 2],
  ]
  const models = [
    ['OpenRouter', '300+ models', '#7C4DFF'],
    ['Claude', 'Anthropic', '#FF6B5B'],
    ['GPT', 'OpenAI', '#2EE59D'],
    ['Grok', 'xAI', 'var(--text)'],
    ['Gemini', 'Google', '#4CC9FF'],
    ['Mistral', 'open weights', '#FFC53D'],
    ['Llama', 'via Ollama', '#3D5BFF'],
    ['LM Studio', 'local', '#FF7AC6'],
    ['Any endpoint', 'OpenAI-compatible', '#A9A1CC'],
  ]
  const note = 'M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z M14 3v5h5 M9 13h6 M9 17h4'
  const globe = 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z M3 12h18 M12 3a14 14 0 0 1 0 18 M12 3a14 14 0 0 0 0 18'
  const sources = [
    ['Roadmap', 'Note · shared with Pip', '#E8E3FF', note, true],
    ['Team handbook', 'Note · shared with Pip and Inky', '#C9B8FF', note, true],
    ['docs.acme.dev', 'Web page · synced', '#B5E9FF', globe, true],
    ['Family recipes', 'Note · shared with Sunny only', '#A8F7D6', note, false],
    ['Weather forecast', 'Web page · synced', '#FFB3A8', globe, true],
  ]
  const faces = [
    ['sun', { eyes: 'wide', mouth: 'open', accessory: 'halo', shape: 'blob', blush: false }, 76],
    ['mint', { eyes: 'visor', mouth: 'flat', accessory: 'sprout', shape: 'squircle', blush: false }, 88],
    ['coral', { eyes: 'wink', mouth: 'grin', accessory: 'crown', shape: 'drop', blush: false }, 96],
    ['plasma', { eyes: 'happy', accessory: 'antenna', shape: 'circle', blush: true }, 120],
    ['sky', { eyes: 'sleepy', accessory: 'headphones', shape: 'circle', blush: true }, 96],
    ['bubblegum', { eyes: 'dots', mouth: 'open', accessory: 'none', shape: 'circle', blush: false }, 88],
    ['ocean', { eyes: 'happy', accessory: 'ring', shape: 'circle', blush: false }, 76],
  ]

  return `
<section class="v2-hero">
  <div class="v2-hero-orbs" aria-hidden="true">
    ${orb({ color: 'sun', shape: 'blob', eyes: 'wide', mouth: 'open', accessory: 'none', blush: false, glow: false, floating: true, size: 52 })}
    ${orb({ size: 76, glow: false, floating: true })}
    ${orb({ color: 'mint', shape: 'squircle', eyes: 'visor', mouth: 'flat', accessory: 'none', blush: false, glow: false, floating: true, size: 52 })}
  </div>
  <h1 class="v2-display">The open-source home for AI companions you actually own</h1>
  <p class="v2-lead">Orbs are little assistants that know your notes and web pages. Run them on any model, host them yourself, and give each one its own face.</p>
  <div class="v2-actions">
    <a class="v2-btn v2-btn-primary" href="${root}get/">Get started free</a>
    <a class="v2-btn v2-btn-quiet" href="${root}get/"><span class="v2-mono v2-dim">$</span> docker run orbs</a>
  </div>
  <span class="v2-fine">MIT licensed · self-hosted · no account with us required</span>
</section>

<section class="v2-wrap v2-pad">
  <div class="v2-showcase">
    <div class="v2-app" role="img" aria-label="The Orbs app: a list of orbs, a chat with Pip and what Pip read to answer">
      <aside class="v2-app-side" aria-hidden="true">
        <span class="v2-label">Your orbs</span>
        ${roster
          .map(
            ([c, eyes, shape, n, s, dot, active]) => `<div class="v2-roster${active ? ' is-active' : ''}">
          ${mini(c, eyes, shape)}
          <span class="v2-roster-text"><span>${n}</span><span>${s}</span></span>
          <span class="v2-dot" style="background:${dot}"></span>
        </div>`,
          )
          .join('')}
      </aside>
      <div class="v2-app-main" aria-hidden="true">
        <div class="v2-app-head"><span class="v2-strong">Pip</span><span class="v2-tag">claude-opus-5-5</span><span class="v2-dim">· Roadmap, Team handbook</span></div>
        <div class="v2-app-chat">
          <div class="v2-bubble">Anything blocking the Q1 launch? Draft a short update for the team.</div>
          <div class="v2-answer">
            ${mini('plasma', 'happy', 'circle')}
            <div class="v2-answer-body">
              <span>Two things are open. The billing migration is still in review, and 3 issues on the roadmap are marked <span class="v2-code">launch-blocker</span>.</span>
              <div class="v2-draft"><b>Draft:</b> Q1 is on track pending billing review (owner: finance) and 3 fixes. ETA next Thursday.</div>
              <div class="v2-cites"><span>Note · Roadmap</span><span>Note · Team handbook</span></div>
            </div>
          </div>
        </div>
        <div class="v2-app-input"><span>Message Pip…</span><span class="v2-send">${icon('up', 16)}</span></div>
      </div>
      <aside class="v2-app-activity" aria-hidden="true">
        <span class="v2-label">Activity</span>
        ${activity
          .map(
            ([i, t, d, tone]) =>
              `<div class="v2-act"><span class="v2-act-icon is-${tone}">${i}</span><span><span class="v2-strong-text">${t}</span><br /><span class="v2-dim">${d}</span></span></div>`,
          )
          .join('')}
      </aside>
    </div>
  </div>
</section>

<section id="jobs" class="v2-wrap v2-section">
  <div class="v2-section-head">
    <h2 class="v2-h2">Give each orb a job</h2>
    <p class="v2-sub">One orb per task works best. Each gets its own model, its own sources and its own face.</p>
  </div>
  <div class="v2-jobs">
    ${jobs
      .map(
        ([c, t, d, look, wash]) => `<div class="v2-card v2-job">
      <div class="v2-wash v2-wash-${wash}">${orb({ color: c, glow: false, size: 84, ...look })}</div>
      <span class="v2-job-title">${t}</span>
      <span class="v2-job-text">${d}</span>
    </div>`,
      )
      .join('')}
  </div>
</section>

<section id="models" class="v2-band">
  <div class="v2-wrap v2-split">
    <div class="v2-split-text">
      <span class="v2-eyebrow">01 · Any model</span>
      <h2 class="v2-h2">Bring your own brain</h2>
      <p class="v2-body">One OpenRouter key gives you hundreds of models. Or use your own provider keys, or run models locally with Ollama. Pick a different model for every orb, and switch whenever something better comes out.</p>
    </div>
    <div class="v2-models">
      ${models
        .map(([n, t, c]) => `<span class="v2-model"><span class="v2-dot" style="background:${c}"></span>${n}<span class="v2-dim">${t}</span></span>`)
        .join('')}
    </div>
  </div>
</section>

<section id="context" class="v2-wrap v2-split v2-split-reverse v2-section-y">
  <div class="v2-sources">
    ${sources
      .map(
        ([n, d, c, path, on]) => `<div class="v2-source">
      <span class="v2-source-icon" style="background:${c}"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#14121F" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${path}"/></svg></span>
      <span class="v2-source-text"><span>${n}</span><span>${d}</span></span>
      <span class="v2-switch${on ? ' is-on' : ''}" aria-hidden="true"><span></span></span>
    </div>`,
      )
      .join('')}
  </div>
  <div class="v2-split-text">
    <span class="v2-eyebrow">02 · Any context</span>
    <h2 class="v2-h2">It knows your stuff. Only the stuff you allow.</h2>
    <p class="v2-body">Add notes, web pages that stay in sync, and files: PDFs, Word documents, Markdown and pictures. Switch sources on per orb, drop a file into a single chat, and every answer shows where it came from.</p>
  </div>
</section>

<section class="v2-wrap v2-section">
  <div class="v2-faces">
    <span class="v2-eyebrow">03 · Any face</span>
    <h2 class="v2-h2">Make it unmistakably yours</h2>
    <div class="v2-faces-row" aria-hidden="true">
      ${faces.map(([c, look, size]) => orb({ color: c, glow: false, floating: true, size, ...look })).join('')}
    </div>
    <p class="v2-body v2-faces-text">4 shapes, 6 eyes, 5 mouths, 7 accessories, 7 colors. Tell your work orb from your kitchen orb at a glance.</p>
  </div>
</section>

<section id="features" class="v2-wrap v2-section">
  <div class="v2-section-head">
    <span class="v2-eyebrow">04 · Does things</span>
    <h2 class="v2-h2">More than a chat box</h2>
    <p class="v2-sub">Orbs remember, act, keep an eye on things and show up where you already talk. Anything that changes something asks you first.</p>
  </div>
  <div class="v2-features">
    ${features
      .map(
        ([title, text]) =>
          `<div class="v2-card v2-feature"><span class="v2-feature-title">${escapeHtml(title)}</span><span class="v2-feature-text">${escapeHtml(text)}</span></div>`,
      )
      .join('')}
  </div>
</section>

<section class="v2-wrap v2-section v2-platforms">
  <div class="v2-card v2-platform">
    <span class="v2-platform-kicker">Web</span>
    <span class="v2-platform-title">Runs in your browser</span>
    <span class="v2-platform-text">Start Orbs and open localhost. Or <a href="${root}demo/">try the live demo</a> first, right in this browser.</span>
  </div>
  <div class="v2-card v2-platform">
    <span class="v2-platform-kicker">Self-hosted</span>
    <span class="v2-platform-title">One Docker command</span>
    <code class="v2-cmd">docker run -p 8080:8080 ghcr.io/stijntersteege/orbs</code>
  </div>
  <div class="v2-card v2-platform">
    <span class="v2-platform-kicker">Mobile</span>
    <span class="v2-platform-title">Talk on the go</span>
    <span class="v2-platform-text">Open your Orbs address on your phone and add it to your home screen. <a href="${root}docs/guides/mobile/">Mobile guide</a></span>
  </div>
</section>

<section class="v2-wrap v2-section">
  <div class="v2-invert">
    <div class="v2-invert-text">
      <span class="v2-eyebrow v2-invert-kicker">Open source · MIT</span>
      <h2 class="v2-h2">Every line is on GitHub. Every feature is free.</h2>
      <p class="v2-body v2-invert-body">Use Orbs at home or at work, change anything, ship your own version. Contributions, connectors and new faces are welcome.</p>
    </div>
    <div class="v2-invert-actions">
      <a class="v2-btn v2-btn-invert" href="${REPO}">Star on GitHub</a>
      <a class="v2-btn v2-btn-invert-quiet" href="${root}open-source/">How to contribute</a>
    </div>
  </div>
</section>

<section class="v2-wrap v2-section v2-faq">
  <h2 class="v2-h2">Questions</h2>
  <div class="v2-faq-list">
    ${FAQ.map(
      ([q, a], i) =>
        `<details${i === 0 ? ' open' : ''}><summary>${escapeHtml(q)}<span class="v2-faq-sign" aria-hidden="true"></span></summary><p>${escapeHtml(a)}</p></details>`,
    ).join('')}
  </div>
</section>

<section class="v2-final">
  ${orb({ color: 'sky', eyes: 'happy', mouth: 'open', accessory: 'headphones', glow: false, floating: true, size: 96 })}
  <h2 class="v2-final-title">Meet your first orb</h2>
  <p class="v2-final-text">Free, open source and running in about two minutes.</p>
  <div class="v2-actions">
    <a class="v2-btn v2-btn-primary" href="${root}get/">Get started</a>
    <a class="v2-btn v2-btn-quiet" href="${root}docs/">Read the docs</a>
  </div>
</section>
`
}
