# Changelog

All notable changes to Orbs are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org).

## [Unreleased]

## [0.1.0] - 2026-11-25

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

[Unreleased]: https://github.com/your-org/orbs/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/your-org/orbs/releases/tag/v0.1.0
