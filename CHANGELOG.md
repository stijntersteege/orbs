# Changelog

All notable changes to Orbs are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org).

## [Unreleased]

### Added

- Accounts. Everyone signs in with a password, a passkey or an OpenID Connect provider (Authentik, Authelia, Keycloak, Google, ...). Orbs, models, context and chats belong to one account and nobody else sees them.
- First-run setup: the first account becomes the admin and keeps everything already on the server.
- Account page: display name, password, passkeys and signed-in devices. Admins manage people under **Accounts**.
- Web page sources refuse private, loopback and link-local addresses (SSRF protection). Admins can allow hosts with `Orbs:Network:AllowedHosts`, block hosts with `Orbs:Network:BlockedHosts`, or allow all private networks with `Orbs:Network:AllowPrivateNetworks`. Downloads are capped at 10 MB.
- Background jobs: a job table and a worker inside the Orbs process that runs due work as the job's owner, with retries (30 s, 60 s, 120 s ...). Settings shows your recent background work. Expired sessions are cleaned up daily. `Orbs:Jobs:Enabled` and `Orbs:Jobs:PollSeconds` control the worker.
- Long chats load their newest 50 messages and page back on request (**Load earlier messages**). `GET /api/conversations/{id}/messages?before=...&limit=...` pages through older ones; `GET /api/conversations/{id}` takes `limit` and says `hasMoreMessages`.
- Tools: orbs can call tools while they answer. A new `ITool` port (next to `IModelAdapter` and `IContextConnector`), `IToolSource` for sets of tools, and function calling in the OpenAI-compatible adapter. Two built-in tools to start: **Current time** and **Calculator**. The Echo demo model tries a tool when you type `/tool <name> {json}`.
- Every tool call, its input and its result streams to the app (`tool_call` and `tool_result` events), shows in the activity panel and is saved with the reply, together with the model that wrote it. `GET /api/tools` lists the tools.
- Tool permissions per orb: **Allow**, **Ask first** (the default) or **Block**, set in the orb editor under "What it can do". Blocked tools are not offered to the model at all.
- Approval cards: when an orb wants a tool that asks first, the reply pauses and the chat shows what it wants to do. **Allow once**, **Always allow** or **Decline**; the reply then continues. Waiting requests survive a reload. `POST /api/conversations/{id}/approvals/{approvalId}` answers one and streams the rest of the reply.
- The audit trail records who let each tool call happen: allowed by the orb settings, approved or declined by you.
- MCP client: connect any Model Context Protocol server under **Tools** and its tools become available to every orb, named `<server>__<tool>`, with the same permissions and approvals. Streamable HTTP with an optional Authorization header (stored encrypted). Local command (stdio) servers only for admins and only when `Orbs:Mcp:AllowStdio` is on. Servers added by non-admins go through the network policy.
- Starter tools, connected under **Tools > Connected apps**: **web search** (SearXNG or Brave), **calendar** over CalDAV (read events, add events), **email** (read the inbox over IMAP; send over SMTP, which orbs ask about first) and **Home Assistant** (list entities, call services). Passwords and tokens are stored encrypted. New `IIntegrationKind` plugins add more.
- Native model adapters for **Anthropic** (Messages API) and **Google Gemini** (generateContent), both streaming and with tool calling. Presets under **Models > Add provider**.
- Memory: orbs save lasting facts during chats with a built-in `remember` tool and drop them with `forget` (both run without asking; you can change that per orb). Memories go into the orb's prompt. A **Memory** tab next to Activity lists them; add, edit or forget any memory there. `/api/orbs/{id}/memories` and `/api/memories/{id}`.
- Shared memories: choose per memory whether it is private to one orb or shared with all your orbs. The Memory tab shows and switches it; orbs can save a fact for all orbs with `remember` (`shared: true`).
- Retrieval: give a provider an **embedding model** and big context sources are split into passages, embedded in the background, and orbs read only the passages that match the question instead of a cut-off copy. Works with OpenAI-compatible endpoints (OpenAI, Ollama, LM Studio, ...) and Gemini; the Echo demo has a simple built-in embedder. The activity panel shows how many passages were read. `POST /api/context-sources/{id}/index` re-indexes a source.
- Files as context: upload PDF, Word (.docx), Markdown, plain text, CSV, JSON or HTML documents and they are read into text (and indexed for retrieval). Upload a picture (PNG, JPEG, GIF, WebP) and orbs whose model can see get the image itself with every question. Up to 20 MB per file. `POST /api/context-sources/files`, `GET /api/context-sources/{id}/file`.
- Schedules and triggers: let an orb work on its own **at set times** (cron with a time zone, such as "every Monday at 9:00"), **when another app calls** a secret webhook (the body goes to the orb), **when a feed has new items** (RSS or Atom) or **when new email arrives** in a connected account. Each schedule writes into its own chat with the orb, and tools still ask first. Manage them on the new **Schedules** page; `/api/schedules`, `POST /api/hooks/{id}/{secret}`.
- Web page sources can keep themselves up to date: hourly, daily or weekly, in the background, re-indexed after every sync.
- Proactive notifications: when a schedule runs, its orb tells you what it wrote (or that it needs your approval) with a Web Push notification that opens the chat. Turn them on per device under **Account > Notifications**, with **quiet hours** in your time zone; notifications that arrive then wait until quiet hours end. The server makes its own VAPID keys (or set `Orbs:Push:VapidPublicKey`, `VapidPrivateKey` and `Subject`). A service worker (`/sw.js`) shows them.
- Channels: talk to your orbs in messaging apps. A new `IChannelAdapter` port, starting with **Telegram**: each orb is its own bot (named after the orb), polled so no public address is needed. Pair a chat by sending a one-time code; everyone else is ignored. Tool approvals work by replying yes or no. The chat shows up in the app as a normal conversation. Manage it on the new **Channels** page, which can also make a PNG of the orb's face for the bot's picture.
- More channels: **Matrix** (joins rooms it is invited to; sets its name and face), **Discord** (one text channel, read over REST), **Signal** (through your own signal-cli REST API) and **email** (its own mailbox, answers by SMTP). The pairing code may now sit anywhere in a message.
- Voice mode: give a provider a speech-to-text and a text-to-speech model, then tap the microphone to talk to an orb and the speaker to have replies read aloud. Works with OpenAI and anything that speaks its audio API, such as a local Whisper server, Kokoro or Piper (openedai-speech). The Echo demo can pretend. `/api/voice`, `/api/voice/transcribe`, `/api/voice/speak`.
- A voice per orb, chosen in the orb editor next to its face, with a preview. Replies read aloud use the orb's voice.
- Animated faces: an orb shows what it is doing. Wide eyes and a pulsing ring while it listens, a pulsing glow while it thinks, a moving mouth while it speaks, and a question mark while it waits for your approval. Reduced-motion settings stop the movement.
- Settings `Orbs:PublicUrl` (where passkeys are valid) and `Orbs:Auth:Oidc:*` (single sign-on).

### Changed

- Every `/api` call needs a signed-in session. `Orbs:ApiToken` still works for scripts and now acts as the first admin.
- SQLite databases now use migrations too, so future versions can change the schema without losing data. Databases from 0.1 and 0.2 are upgraded in place on start.

## [0.2.0] - 2026-11-26

A makeover: a cleaner look for the app and the website, your orbs always at hand, and an activity panel that shows what an orb read and which model answered.

### Added

- Documentation on the website: guides, the arc42 architecture and the decision records, generated from `docs/` on every push to `main`.
- CI checks that the website builds and that links in the docs and changelog are not broken.
- New website design based on the Orbs design system: home page, Get Orbs, Open source, light and dark themes.
- Policy pages on the website (privacy, security, code of conduct, contributing, license), generated from the files in the repository.
- `PRIVACY.md`: what happens with your data in the software and on the website.
- Light/dark switch on the website. The choice is shared with the live demo and remembered in the browser.
- Model picker at the top of every chat: search the models of every connected provider, or pick "Auto". The choice is saved on the orb.
- Activity panel in the app: for every reply, the sources the orb read and the model and provider that wrote it, with the time it took. Answers list the sources they read.
- The `start` event of a streamed reply now includes the provider name and the sources sent to the model (`provider`, `sources`).

### Changed

- New design for the app and the website: clean neutral light and dark themes, the orb colors as the only accents, Geist and Geist Mono as fonts, quieter buttons, cards and borders.
- The app opens on a chat instead of the orb overview. Models, Context, orb management and Settings moved behind one "Settings and more" menu.
- The app's sidebar always shows your orbs, with a status dot each. Earlier chats moved to a **Chats** menu in the chat header. On phones the sidebar slides in from a menu button, replacing the bottom tab bar.
- New website home page: product preview, use cases, any model, any context, any face, and a FAQ.
- `SECURITY.md`, `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md` and the README now speak in a personal voice and make no promises about response times: Orbs is a hobby project.

## [0.1.0] - 2026-11-26

### Added

- Orbs with a name, description, brief, model choice ("Auto" or pinned) and a customizable face (color, shape, eyes, mouth, accessory, blush, glow, float).
- Model providers as plugins: an OpenAI-compatible adapter (OpenAI, OpenRouter, Ollama, LM Studio, vLLM, LiteLLM, ...) and an Echo demo adapter.
- Context sources as plugins: notes and web pages, linked per orb.
- Streaming chat over server-sent events, with conversation history per orb.
- Encrypted provider API keys and an optional API token for the whole API.
- Web app with light and dark themes, responsive layout and a PWA manifest; Capacitor configuration for iOS and Android.
- One Docker image for API and web app; Docker Compose with PostgreSQL; SQLite for zero-setup installs.
- Test suites: domain, application and infrastructure unit tests, API integration tests, architecture tests, Reqnroll specifications, Vitest and Playwright.
- Documentation: arc42 architecture, ADRs, guides, contribution and security policies.
- Project website on GitHub Pages with a landing page, the changelog and a live demo that runs on sample data in the browser.

[Unreleased]: https://github.com/stijntersteege/orbs/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/stijntersteege/orbs/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/stijntersteege/orbs/releases/tag/v0.1.0
