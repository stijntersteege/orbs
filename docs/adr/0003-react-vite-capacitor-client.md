# 0003. One React client for web and mobile

- **Status**: Accepted
- **Date**: 2026-11-25

## Context

Orbs must work in desktop browsers and on phones. The team is small; maintaining separate web, iOS and Android clients is not realistic.

## Options

1. **Separate native apps** (Swift, Kotlin) plus a web app: best native feel, triple the work.
2. **Flutter**: one codebase, but web output is weaker and the ecosystem is separate from the web.
3. **React Native with Expo** (and React Native Web): native widgets, but web is a second-class target and testing tooling is heavier.
4. **React + Vite as a responsive web app, wrapped with Capacitor** for app stores, plus a PWA manifest: one codebase, full web platform, standard web testing (Vitest, Playwright).

## Decision

Option 4. A React 19 + TypeScript app built with Vite, TanStack Query for server state, React Router for navigation, CSS Modules on design tokens. Capacitor provides iOS and Android shells around the same build.

## Consequences

- One UI codebase, one design system, one test setup.
- Responsive design is mandatory: every screen must work at phone width (sidebar becomes a tab bar).
- Native capabilities (push, microphone for voice) come through Capacitor plugins when needed.
- The mobile app talks to a remote server, so the server address is a user setting.
