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
