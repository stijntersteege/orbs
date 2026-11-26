# Changelog

All notable changes to Orbs are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org).

## [Unreleased]

### Added

- Documentation on the website: guides, the arc42 architecture and the decision records, generated from `docs/` on every push to `main`.
- CI checks that the website builds and that links in the docs and changelog are not broken.
- New website design based on the Orbs design system: home page, Get Orbs, Open source, light and dark themes.
- Policy pages on the website (privacy, security, code of conduct, contributing, license), generated from the files in the repository.
- `PRIVACY.md`: what happens with your data in the software and on the website.

### Changed

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

[Unreleased]: https://github.com/stijntersteege/orbs/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/stijntersteege/orbs/releases/tag/v0.1.0
